using System.Text.Json;
using System.Globalization;
using HashProcessor.Api;
using HashProcessor.Contracts;

namespace HashProcessor.Tests.Integration;

[Collection(RabbitMqTestGroup.Name)]
public class HashPublisherTests
{
    private readonly RabbitMqFixture _fixture;

    public HashPublisherTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublishAsyncPublishesPersistentJsonMessages()
    {
        await _fixture.ResetAsync();
        var messages = Enumerable.Range(1, 3)
            .Select(index => new HashGeneratedMessage
            {
                Sha1 = index.ToString("x40", CultureInfo.InvariantCulture),
                GeneratedAtUtc = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc)
            })
            .ToList();

        await using var publisher = new HashPublisher(_fixture.ConnectionString);

        await publisher.PublishAsync(messages, CancellationToken.None);

        for (var index = 0; index < messages.Count; index++)
        {
            var result = await _fixture.GetMessageAsync(HashQueue.Name, TimeSpan.FromSeconds(5));

            Assert.NotNull(result);
            Assert.Equal("application/json", result.BasicProperties.ContentType);
            Assert.True(result.BasicProperties.Persistent);

            var actual = JsonSerializer.Deserialize<HashGeneratedMessage>(result.Body.Span);

            Assert.NotNull(actual);
            Assert.Equal(messages[index].Sha1, actual.Sha1);
            Assert.Equal(messages[index].GeneratedAtUtc, actual.GeneratedAtUtc);
        }

        Assert.Null(await _fixture.GetMessageAsync(HashQueue.Name, TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public async Task PublishAsyncCanReusePublisherAcrossCalls()
    {
        await _fixture.ResetAsync();
        await using var publisher = new HashPublisher(_fixture.ConnectionString);

        await publisher.PublishAsync([CreateMessage("1111111111111111111111111111111111111111")], CancellationToken.None);
        await publisher.PublishAsync([CreateMessage("2222222222222222222222222222222222222222")], CancellationToken.None);

        Assert.NotNull(await _fixture.GetMessageAsync(HashQueue.Name, TimeSpan.FromSeconds(5)));
        Assert.NotNull(await _fixture.GetMessageAsync(HashQueue.Name, TimeSpan.FromSeconds(5)));
        Assert.Null(await _fixture.GetMessageAsync(HashQueue.Name, TimeSpan.FromMilliseconds(250)));
    }

    private static HashGeneratedMessage CreateMessage(string sha1)
    {
        return new HashGeneratedMessage
        {
            Sha1 = sha1,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
