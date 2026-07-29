using System.Text.Json;
using HashProcessor.Contracts;
using HashProcessor.Database;

namespace HashProcessor.Worker;

public class HashMessageProcessor
{
    private readonly IHashRepository _hashRepository;

    public HashMessageProcessor(IHashRepository hashRepository)
    {
        _hashRepository = hashRepository;
    }

    public async Task ProcessAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<HashGeneratedMessage>(body.Span) ?? throw new JsonException("The RabbitMQ message body is empty.");

        await _hashRepository.SaveHashAsync(message.Sha1, message.GeneratedAtUtc, cancellationToken);
    }
}
