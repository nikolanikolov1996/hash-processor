using HashProcessor.Contracts;
using HashProcessor.Messaging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace HashProcessor.Tests.Integration;

public class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4.3.4-management")
        .WithUsername("hash_processor")
        .WithPassword("hash_processor_test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var connection = await CreateConnectionFactory().CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await HashQueueTopology.DeclareAsync(channel, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public ConnectionFactory CreateConnectionFactory()
    {
        return new ConnectionFactory
        {
            Uri = new Uri(ConnectionString),
            AutomaticRecoveryEnabled = true
        };
    }

    public async Task ResetAsync()
    {
        await using var connection = await CreateConnectionFactory().CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueuePurgeAsync(HashQueue.Name);
        await channel.QueuePurgeAsync(HashQueue.DeadLetterQueueName);
    }

    public async Task<BasicGetResult?> GetMessageAsync(string queueName, TimeSpan timeout)
    {
        await using var connection = await CreateConnectionFactory().CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        using var cancellationTokenSource = new CancellationTokenSource(timeout);

        try
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var result = await channel.BasicGetAsync(queueName, autoAck: true, cancellationTokenSource.Token);

                if (result is not null)
                {
                    return result;
                }

                await Task.Delay(100, cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            return null;
        }

        return null;
    }
}
