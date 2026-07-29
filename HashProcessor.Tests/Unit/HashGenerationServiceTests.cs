using HashProcessor.Api.Services;
using HashProcessor.Tests.Fakes;

namespace HashProcessor.Tests.Unit;

public class HashGenerationServiceTests
{
    [Fact]
    public async Task GenerateAndPublishHashesAsyncPublishesFortyThousandValidHashes()
    {
        var publisher = new FakeHashPublisher();
        var service = new HashGenerationService(publisher);

        await service.GenerateAndPublishHashesAsync(CancellationToken.None);

        Assert.Equal(40_000, publisher.PublishedMessages.Count);
        Assert.Equal(40_000, publisher.PublishedMessages.Select(message => message.Sha1).Distinct().Count());
        Assert.All(publisher.PublishedMessages, message => Assert.Matches("^[0-9a-f]{40}$", message.Sha1));
        Assert.All(publisher.PublishedMessages, message => Assert.Equal(DateTimeKind.Utc, message.GeneratedAtUtc.Kind));
        Assert.Single(publisher.PublishedMessages.Select(message => message.GeneratedAtUtc).Distinct());
    }

    [Fact]
    public async Task GenerateAndPublishHashesAsyncForwardsCancellationToken()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var publisher = new FakeHashPublisher();
        var service = new HashGenerationService(publisher);

        await service.GenerateAndPublishHashesAsync(cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, publisher.CancellationToken);
    }

    [Fact]
    public async Task GenerateAndPublishHashesAsyncPropagatesPublisherFailure()
    {
        var publisher = new FakeHashPublisher
        {
            PublishException = new InvalidOperationException("RabbitMQ is unavailable.")
        };

        var service = new HashGenerationService(publisher);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAndPublishHashesAsync(CancellationToken.None));

        Assert.Equal("RabbitMQ is unavailable.", exception.Message);
    }
}
