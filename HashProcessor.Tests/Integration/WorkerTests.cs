using System.Text;
using System.Text.Json;
using System.Globalization;
using HashProcessor.Contracts;
using HashProcessor.Tests.Fakes;
using HashProcessor.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using HashWorker = HashProcessor.Worker.Worker;

namespace HashProcessor.Tests.Integration;

[Collection(RabbitMqTestGroup.Name)]
public class WorkerTests
{
    private readonly RabbitMqFixture _fixture;

    public WorkerTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task WorkerMovesInvalidMessageToDeadLetterQueue()
    {
        await _fixture.ResetAsync();
        var repository = new FakeHashRepository();
        var messageProcessor = new HashMessageProcessor(repository);
        using var worker = new HashWorker(NullLogger<HashWorker>.Instance, messageProcessor, _fixture.CreateConnectionFactory());

        await worker.StartAsync(CancellationToken.None);

        try
        {
            await PublishRawMessageAsync(Encoding.UTF8.GetBytes("{not-json}"));

            var failedMessage = await _fixture.GetMessageAsync(HashQueue.DeadLetterQueueName, TimeSpan.FromSeconds(10));

            Assert.NotNull(failedMessage);
            Assert.Equal(0, repository.SaveAttemptCount);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WorkerRetriesTransientFailureThreeTimesThenDeadLettersMessage()
    {
        await _fixture.ResetAsync();
        var repository = new FakeHashRepository
        {
            SaveException = new InvalidOperationException("MariaDB is unavailable.")
        };

        var messageProcessor = new HashMessageProcessor(repository);
        using var worker = new HashWorker(NullLogger<HashWorker>.Instance, messageProcessor, _fixture.CreateConnectionFactory());

        await worker.StartAsync(CancellationToken.None);

        try
        {
            var message = new HashGeneratedMessage
            {
                Sha1 = "0123456789abcdef0123456789abcdef01234567",
                GeneratedAtUtc = DateTime.UtcNow
            };

            await PublishRawMessageAsync(JsonSerializer.SerializeToUtf8Bytes(message));

            var failedMessage = await _fixture.GetMessageAsync(HashQueue.DeadLetterQueueName, TimeSpan.FromSeconds(15));

            Assert.NotNull(failedMessage);
            Assert.Equal(HashQueue.MaxRetryCount + 1, repository.SaveAttemptCount);
            Assert.NotNull(failedMessage.BasicProperties.Headers);
            Assert.Equal(HashQueue.MaxRetryCount,
                         Convert.ToInt32(failedMessage.BasicProperties.Headers[HashQueue.RetryHeaderName], CultureInfo.InvariantCulture));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WorkerAcknowledgesMessageAfterTransientRetrySucceeds()
    {
        await _fixture.ResetAsync();
        var repository = new FakeHashRepository
        {
            SaveException = new InvalidOperationException("MariaDB was temporarily unavailable."),
            FailuresBeforeSuccess = 1
        };

        var messageProcessor = new HashMessageProcessor(repository);
        using var worker = new HashWorker(NullLogger<HashWorker>.Instance, messageProcessor, _fixture.CreateConnectionFactory());

        await worker.StartAsync(CancellationToken.None);

        try
        {
            var message = new HashGeneratedMessage
            {
                Sha1 = "0123456789abcdef0123456789abcdef01234567",
                GeneratedAtUtc = DateTime.UtcNow
            };

            await PublishRawMessageAsync(JsonSerializer.SerializeToUtf8Bytes(message));

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (repository.SuccessfulSaveCount == 0)
            {
                await Task.Delay(100, cancellationTokenSource.Token);
            }

            Assert.Equal(2, repository.SaveAttemptCount);
            Assert.Equal(1, repository.SuccessfulSaveCount);
            Assert.Null(await _fixture.GetMessageAsync(HashQueue.Name, TimeSpan.FromMilliseconds(250)));
            Assert.Null(await _fixture.GetMessageAsync(HashQueue.DeadLetterQueueName, TimeSpan.FromMilliseconds(250)));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private async Task PublishRawMessageAsync(ReadOnlyMemory<byte> body)
    {
        await using var connection = await _fixture.CreateConnectionFactory().CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true
        };

        await channel.BasicPublishAsync(exchange: string.Empty,
                                        routingKey: HashQueue.Name,
                                        mandatory: true,
                                        basicProperties: properties,
                                        body: body);
    }
}
