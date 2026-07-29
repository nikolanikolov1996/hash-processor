using System.Globalization;
using HashProcessor.Database;

namespace HashProcessor.Tests.Integration;

[Collection(MariaDbTestGroup.Name)]
public class HashDatabaseTests
{
    private readonly MariaDbFixture _fixture;

    public HashDatabaseTests(MariaDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveHashAsyncPersistsHashAndUpdatesDailyCount()
    {
        await _fixture.ResetAsync();
        var database = new HashDatabase(_fixture.ConnectionString);
        var generatedAtUtc = new DateTime(2026, 7, 29, 10, 15, 30, DateTimeKind.Utc);

        await database.SaveHashAsync("0123456789abcdef0123456789abcdef01234567", generatedAtUtc, CancellationToken.None);

        var hashCounts = await database.GetHashCountsByDateAsync(CancellationToken.None);
        var hashCount = Assert.Single(hashCounts);

        Assert.Equal(1, await _fixture.GetHashRowCountAsync());
        Assert.Equal(new DateOnly(2026, 7, 29), hashCount.Date);
        Assert.Equal(1, hashCount.Count);
    }

    [Fact]
    public async Task SaveHashAsyncDoesNotCountDuplicateSha1Twice()
    {
        await _fixture.ResetAsync();
        var database = new HashDatabase(_fixture.ConnectionString);
        const string sha1 = "0123456789abcdef0123456789abcdef01234567";

        await database.SaveHashAsync(sha1, new DateTime(2026, 7, 28, 23, 59, 0, DateTimeKind.Utc), CancellationToken.None);
        await database.SaveHashAsync(sha1, new DateTime(2026, 7, 29, 0, 1, 0, DateTimeKind.Utc), CancellationToken.None);

        var hashCounts = await database.GetHashCountsByDateAsync(CancellationToken.None);
        var hashCount = Assert.Single(hashCounts);

        Assert.Equal(1, await _fixture.GetHashRowCountAsync());
        Assert.Equal(new DateOnly(2026, 7, 28), hashCount.Date);
        Assert.Equal(1, hashCount.Count);
    }

    [Fact]
    public async Task GetHashCountsByDateAsyncReturnsStoredSummaryInDateOrder()
    {
        await _fixture.ResetAsync();
        var database = new HashDatabase(_fixture.ConnectionString);

        await database.SaveHashAsync("1111111111111111111111111111111111111111", new DateTime(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        await database.SaveHashAsync("2222222222222222222222222222222222222222", new DateTime(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        await database.SaveHashAsync("3333333333333333333333333333333333333333", new DateTime(2026, 7, 30, 9, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        var hashCounts = await database.GetHashCountsByDateAsync(CancellationToken.None);

        Assert.Collection(hashCounts,
                          first =>
                          {
                              Assert.Equal(new DateOnly(2026, 7, 29), first.Date);
                              Assert.Equal(1, first.Count);
                          },
                          second =>
                          {
                              Assert.Equal(new DateOnly(2026, 7, 30), second.Date);
                              Assert.Equal(2, second.Count);
                          });
    }

    [Fact]
    public async Task SaveHashAsyncMaintainsCorrectCountUnderConcurrency()
    {
        await _fixture.ResetAsync();
        var database = new HashDatabase(_fixture.ConnectionString);
        var generatedAtUtc = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

        var saveTasks = Enumerable.Range(0, 100)
            .Select(index => index.ToString("x40", CultureInfo.InvariantCulture))
            .Select(sha1 => database.SaveHashAsync(sha1, generatedAtUtc, CancellationToken.None));

        await Task.WhenAll(saveTasks);

        var hashCounts = await database.GetHashCountsByDateAsync(CancellationToken.None);
        var hashCount = Assert.Single(hashCounts);

        Assert.Equal(100, await _fixture.GetHashRowCountAsync());
        Assert.Equal(100, hashCount.Count);
    }
}
