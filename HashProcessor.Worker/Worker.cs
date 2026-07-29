using System.Text.Json;
using HashProcessor.Contracts;
using HashProcessor.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HashProcessor.Worker;

public partial class Worker : BackgroundService
{
    private const int ConsumerCount = 4;

    private readonly ILogger<Worker> _logger;
    private readonly HashMessageProcessor _messageProcessor;
    private readonly ConnectionFactory _connectionFactory;

    public Worker(ILogger<Worker> logger,
                  HashMessageProcessor messageProcessor,
                  ConnectionFactory connectionFactory)
    {
        _logger = logger;
        _messageProcessor = messageProcessor;
        _connectionFactory = connectionFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync("hash-processor-worker", stoppingToken);
        var channels = new List<IChannel>(ConsumerCount);

        try
        {
            for (var index = 0; index < ConsumerCount; index++)
            {
                var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true,
                                                              publisherConfirmationTrackingEnabled: true);

                var channel = await connection.CreateChannelAsync(channelOptions, stoppingToken);
                channels.Add(channel);

                await HashQueueTopology.DeclareAsync(channel, stoppingToken);

                await channel.BasicQosAsync(prefetchSize: 0,
                                            prefetchCount: 1,
                                            global: false,
                                            cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, eventArgs) => ProcessMessageAsync(channel, eventArgs, stoppingToken);

                await channel.BasicConsumeAsync(queue: HashQueue.Name,
                                                autoAck: false,
                                                consumerTag: $"hash-processor-worker-{index + 1}",
                                                noLocal: false,
                                                exclusive: false,
                                                arguments: null,
                                                consumer: consumer,
                                                cancellationToken: stoppingToken);
            }

            LogConsumersStarted(ConsumerCount);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        finally
        {
            foreach (var channel in channels)
            {
                await channel.DisposeAsync();
            }
        }
    }

    private async Task ProcessMessageAsync(IChannel channel, BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        try
        {
            await _messageProcessor.ProcessAsync(eventArgs.Body, cancellationToken);
            await channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            LogInvalidMessage(exception);
            await channel.BasicRejectAsync(deliveryTag: eventArgs.DeliveryTag, requeue: false, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The unacknowledged message is returned to the queue when the channel closes.
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            await RetryOrDeadLetterAsync(channel, eventArgs, exception, cancellationToken);
        }
    }

    private async Task RetryOrDeadLetterAsync(IChannel channel, BasicDeliverEventArgs eventArgs, Exception exception, CancellationToken cancellationToken)
    {
        var retryCount = GetRetryCount(eventArgs.BasicProperties);

        if (retryCount >= HashQueue.MaxRetryCount)
        {
            LogMessageDeadLettered(exception, HashQueue.DeadLetterQueueName, retryCount);

            await channel.BasicRejectAsync(deliveryTag: eventArgs.DeliveryTag,
                                           requeue: false,
                                           cancellationToken: cancellationToken);

            return;
        }

        var nextRetryCount = retryCount + 1;
        var retryDelay = TimeSpan.FromSeconds(1 << retryCount);

        LogMessageRetry(exception,
                        nextRetryCount,
                        HashQueue.MaxRetryCount,
                        retryDelay.TotalSeconds);

        await Task.Delay(retryDelay, cancellationToken);

        var properties = new BasicProperties(eventArgs.BasicProperties)
        {
            Persistent = true,
            Headers = eventArgs.BasicProperties.Headers is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(eventArgs.BasicProperties.Headers)
        };

        properties.Headers[HashQueue.RetryHeaderName] = nextRetryCount;

        try
        {
            await channel.BasicPublishAsync(exchange: string.Empty,
                                            routingKey: HashQueue.Name,
                                            mandatory: true,
                                            basicProperties: properties,
                                            body: eventArgs.Body,
                                            cancellationToken: cancellationToken);

            await channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag,
                                        multiple: false,
                                        cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The original unacknowledged message is returned to the queue when the channel closes.
        }
        catch (Exception retryException)
        {
            LogRetryPublishFailure(retryException);

            await channel.BasicNackAsync(deliveryTag: eventArgs.DeliveryTag,
                                         multiple: false,
                                         requeue: true,
                                         cancellationToken: cancellationToken);
        }
    }

    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null ||
            !properties.Headers.TryGetValue(HashQueue.RetryHeaderName, out var value))
        {
            return 0;
        }

        var retryCount = value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number when number <= int.MaxValue => (int)number,
            _ => HashQueue.MaxRetryCount
        };

        return Math.Max(0, retryCount);
    }

    [LoggerMessage(EventId = 1,
                   Level = LogLevel.Information,
                   Message = "Started {ConsumerCount} RabbitMQ consumers.")]
    private partial void LogConsumersStarted(int consumerCount);

    [LoggerMessage(EventId = 2,
                   Level = LogLevel.Warning,
                   Message = "Rejected an invalid RabbitMQ message.")]
    private partial void LogInvalidMessage(Exception exception);

    [LoggerMessage(EventId = 3,
                   Level = LogLevel.Error,
                   Message = "Moved a RabbitMQ message to {DeadLetterQueueName} after {RetryCount} retries.")]
    private partial void LogMessageDeadLettered(Exception exception, string deadLetterQueueName, int retryCount);

    [LoggerMessage(EventId = 4,
                   Level = LogLevel.Warning,
                   Message = "Failed to process a RabbitMQ message. Retrying {RetryCount}/{MaxRetryCount} in {RetryDelaySeconds} seconds.")]
    private partial void LogMessageRetry(Exception exception, int retryCount, int maxRetryCount, double retryDelaySeconds);

    [LoggerMessage(EventId = 5,
                   Level = LogLevel.Error,
                   Message = "Failed to republish a RabbitMQ message for retry.")]
    private partial void LogRetryPublishFailure(Exception exception);
}
