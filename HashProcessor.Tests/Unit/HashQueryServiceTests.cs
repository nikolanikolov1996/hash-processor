using HashProcessor.Api.Services;
using HashProcessor.Contracts;
using HashProcessor.Tests.Fakes;

namespace HashProcessor.Tests.Unit;

public class HashQueryServiceTests
{
    [Fact]
    public async Task GetHashCountsByDateAsyncReturnsRepositoryResult()
    {
        var expected = new List<HashCountByDate>
        {
            new()
            {
                Date = new DateOnly(2026, 7, 28),
                Count = 10
            },
            new()
            {
                Date = new DateOnly(2026, 7, 29),
                Count = 20
            }
        };

        using var cancellationTokenSource = new CancellationTokenSource();
        var repository = new FakeHashRepository
        {
            HashCounts = expected
        };

        var service = new HashQueryService(repository);

        var actual = await service.GetHashCountsByDateAsync(cancellationTokenSource.Token);

        Assert.Same(expected, actual);
        Assert.Equal(cancellationTokenSource.Token, repository.CancellationToken);
    }
}
