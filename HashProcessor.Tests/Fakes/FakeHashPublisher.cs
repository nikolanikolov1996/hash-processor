using HashProcessor.Api;
using HashProcessor.Contracts;

namespace HashProcessor.Tests.Fakes;

public class FakeHashPublisher : IHashPublisher
{
    public List<HashGeneratedMessage> PublishedMessages { get; } = [];

    public CancellationToken CancellationToken { get; private set; }

    public Exception? PublishException { get; set; }

    public Task PublishAsync(IEnumerable<HashGeneratedMessage> messages, CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;

        if (PublishException is not null)
        {
            throw PublishException;
        }

        PublishedMessages.AddRange(messages);

        return Task.CompletedTask;
    }
}
