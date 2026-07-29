using HashProcessor.Contracts;
using RabbitMQ.Client;

namespace HashProcessor.Messaging;

public static class HashQueueTopology
{
    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(exchange: HashQueue.DeadLetterExchangeName,
                                           type: ExchangeType.Direct,
                                           durable: true,
                                           autoDelete: false,
                                           arguments: null,
                                           cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(queue: HashQueue.DeadLetterQueueName,
                                        durable: true,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: null,
                                        cancellationToken: cancellationToken);

        await channel.QueueBindAsync(queue: HashQueue.DeadLetterQueueName,
                                     exchange: HashQueue.DeadLetterExchangeName,
                                     routingKey: HashQueue.DeadLetterRoutingKey,
                                     arguments: null,
                                     cancellationToken: cancellationToken);

        var queueArguments = new Dictionary<string, object?>
        {
            [Headers.XDeadLetterExchange] = HashQueue.DeadLetterExchangeName,
            [Headers.XDeadLetterRoutingKey] = HashQueue.DeadLetterRoutingKey
        };

        await channel.QueueDeclareAsync(queue: HashQueue.Name,
                                        durable: true,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: queueArguments,
                                        cancellationToken: cancellationToken);
    }
}
