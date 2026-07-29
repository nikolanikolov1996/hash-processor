using HashProcessor.Api.Services;
using HashProcessor.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HashProcessor.Api.Controllers;

[ApiController]
[Route("hashes")]
public class HashesController : ControllerBase
{
    private readonly HashGenerationService _hashGenerationService;
    private readonly HashQueryService _hashQueryService;

    public HashesController(HashGenerationService hashGenerationService, HashQueryService hashQueryService)
    {
        _hashGenerationService = hashGenerationService;
        _hashQueryService = hashQueryService;
    }

    [HttpPost]
    [EnableRateLimiting("hash-generation")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GenerateHashes(CancellationToken cancellationToken)
    {
        await _hashGenerationService.GenerateAndPublishHashesAsync(cancellationToken);

        return Accepted();
    }

    [HttpGet]
    [ProducesResponseType<HashesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HashesResponse>> GetHashCounts(CancellationToken cancellationToken)
    {
        var hashCounts = await _hashQueryService.GetHashCountsByDateAsync(cancellationToken);

        return Ok(new HashesResponse
        {
            Hashes = hashCounts
        });
    }
}
