using System.Text;
using System.Text.Json;
using HashProcessor.Contracts;
using HashProcessor.Tests.Fakes;
using HashProcessor.Worker;

namespace HashProcessor.Tests.Unit;

public class HashMessageProcessorTests
{
    [Fact]
    public async Task ProcessAsyncSavesDeserializedMessage()
    {
        var generatedAtUtc = new DateTime(2026, 7, 29, 12, 30, 0, DateTimeKind.Utc);
        var message = new HashGeneratedMessage
        {
            Sha1 = "0123456789abcdef0123456789abcdef01234567",
            GeneratedAtUtc = generatedAtUtc
        };

        using var cancellationTokenSource = new CancellationTokenSource();
        var repository = new FakeHashRepository();
        var processor = new HashMessageProcessor(repository);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await processor.ProcessAsync(body, cancellationTokenSource.Token);

        var savedHash = Assert.Single(repository.SavedHashes);
        Assert.Equal(message.Sha1, savedHash.Sha1);
        Assert.Equal(generatedAtUtc, savedHash.GeneratedAtUtc);
        Assert.Equal(cancellationTokenSource.Token, repository.CancellationToken);
    }

    [Fact]
    public async Task ProcessAsyncRejectsMalformedJson()
    {
        var repository = new FakeHashRepository();
        var processor = new HashMessageProcessor(repository);
        var body = Encoding.UTF8.GetBytes("{not-json}");

        await Assert.ThrowsAsync<JsonException>(() => processor.ProcessAsync(body, CancellationToken.None));

        Assert.Empty(repository.SavedHashes);
    }

    [Fact]
    public async Task ProcessAsyncRejectsEmptyMessage()
    {
        var repository = new FakeHashRepository();
        var processor = new HashMessageProcessor(repository);
        var body = Encoding.UTF8.GetBytes("null");

        var exception = await Assert.ThrowsAsync<JsonException>(() => processor.ProcessAsync(body, CancellationToken.None));

        Assert.Equal("The RabbitMQ message body is empty.", exception.Message);
        Assert.Empty(repository.SavedHashes);
    }

    [Fact]
    public async Task ProcessAsyncPropagatesRepositoryFailure()
    {
        var repository = new FakeHashRepository
        {
            SaveException = new InvalidOperationException("MariaDB is unavailable.")
        };

        var processor = new HashMessageProcessor(repository);
        var body = JsonSerializer.SerializeToUtf8Bytes(new HashGeneratedMessage
        {
            Sha1 = "0123456789abcdef0123456789abcdef01234567",
            GeneratedAtUtc = DateTime.UtcNow
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(body, CancellationToken.None));

        Assert.Equal("MariaDB is unavailable.", exception.Message);
        Assert.Equal(1, repository.SaveAttemptCount);
    }
}
