using HashProcessor.Contracts;

namespace HashProcessor.Api;

public interface IHashPublisher
{
    Task PublishAsync(IEnumerable<HashGeneratedMessage> messages, CancellationToken cancellationToken);
}
