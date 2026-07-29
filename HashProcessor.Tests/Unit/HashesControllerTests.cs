using HashProcessor.Api.Controllers;
using HashProcessor.Api.Services;
using HashProcessor.Contracts;
using HashProcessor.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;

namespace HashProcessor.Tests.Unit;

public class HashesControllerTests
{
    [Fact]
    public async Task GenerateHashesReturnsAcceptedAfterPublishing()
    {
        var publisher = new FakeHashPublisher();
        var repository = new FakeHashRepository();
        var controller = CreateController(publisher, repository);

        var result = await controller.GenerateHashes(CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        Assert.Equal(40_000, publisher.PublishedMessages.Count);
    }

    [Fact]
    public async Task GetHashCountsReturnsExplicitResponseContract()
    {
        var expected = new List<HashCountByDate>
        {
            new()
            {
                Date = new DateOnly(2026, 7, 29),
                Count = 40_000
            }
        };

        var publisher = new FakeHashPublisher();
        var repository = new FakeHashRepository
        {
            HashCounts = expected
        };

        var controller = CreateController(publisher, repository);

        var actionResult = await controller.GetHashCounts(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<HashesResponse>(okResult.Value);

        Assert.Same(expected, response.Hashes);
    }

    private static HashesController CreateController(FakeHashPublisher publisher, FakeHashRepository repository)
    {
        return new HashesController(new HashGenerationService(publisher),
                                    new HashQueryService(repository));
    }
}
