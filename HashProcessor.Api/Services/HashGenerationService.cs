using System.Security.Cryptography;
using HashProcessor.Contracts;

namespace HashProcessor.Api.Services;

public class HashGenerationService
{
    private const int HashCount = 40_000;
    private readonly IHashPublisher _hashPublisher;

    public HashGenerationService(IHashPublisher hashPublisher)
    {
        _hashPublisher = hashPublisher;
    }

    public async Task GenerateAndPublishHashesAsync(CancellationToken cancellationToken)
    {
        await _hashPublisher.PublishAsync(GenerateHashes(), cancellationToken);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350:Do not use weak cryptographic algorithms", Justification = "SHA1 output is required by the challenge and is not used for security.")]
    private static IEnumerable<HashGeneratedMessage> GenerateHashes()
    {
        var generatedAtUtc = DateTime.UtcNow;

        for (var index = 0; index < HashCount; index++)
        {
            var randomBytes = RandomNumberGenerator.GetBytes(32);
            var sha1Bytes = SHA1.HashData(randomBytes);
            var sha1 = Convert.ToHexString(sha1Bytes).ToLowerInvariant();

            yield return new HashGeneratedMessage
            {
                Sha1 = sha1,
                GeneratedAtUtc = generatedAtUtc
            };
        }
    }
}
