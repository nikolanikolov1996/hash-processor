using System.Text.Json;
using HashProcessor.Contracts;
using HashProcessor.Messaging;
using RabbitMQ.Client;

namespace HashProcessor.Api;

public class HashPublisher : IHashPublisher, IAsyncDisposable
{
    private const ushort MaxOutstandingConfirmations = 256;
    private const int ConfirmationBatchSize = MaxOutstandingConfirmations / 2;

    private readonly ConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public HashPublisher(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("The RabbitMQ connection string is required.", nameof(connectionString));
        }

        _connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            AutomaticRecoveryEnabled = true
        };
    }

    public async Task PublishAsync(IEnumerable<HashGeneratedMessage> messages, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);

        var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true,
                                                      publisherConfirmationTrackingEnabled: true,
                                                      outstandingPublisherConfirmationsRateLimiter: new ThrottlingRateLimiter(MaxOutstandingConfirmations));

        await using var channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);

        await HashQueueTopology.DeclareAsync(channel, cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true
        };

        var publishTasks = new List<Task>(ConfirmationBatchSize);

        foreach (var message in messages)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(message);

            var publishTask = channel.BasicPublishAsync(exchange: string.Empty,
                                                        routingKey: HashQueue.Name,
                                                        mandatory: true,
                                                        basicProperties: properties,
                                                        body: body,
                                                        cancellationToken: cancellationToken).AsTask();

            publishTasks.Add(publishTask);

            if (publishTasks.Count == ConfirmationBatchSize)
            {
                await WaitForPublisherConfirmationsAsync(publishTasks);
            }
        }

        await WaitForPublisherConfirmationsAsync(publishTasks);
    }

    public async ValueTask DisposeAsync()
    {
        await _connectionLock.WaitAsync();

        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        finally
        {
            _connectionLock.Release();
            _connectionLock.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection?.IsOpen == true)
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (_connection?.IsOpen == true)
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            _connection = await _connectionFactory.CreateConnectionAsync("hash-processor-api", cancellationToken);

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private static async Task WaitForPublisherConfirmationsAsync(List<Task> publishTasks)
    {
        await Task.WhenAll(publishTasks);
        publishTasks.Clear();
    }
}
