using HashProcessor.Database;

using HashProcessor.Contracts;

namespace HashProcessor.Api.Services;

public class HashQueryService
{
    private readonly IHashRepository _hashRepository;

    public HashQueryService(IHashRepository hashRepository)
    {
        _hashRepository = hashRepository;
    }

    public Task<IReadOnlyList<HashCountByDate>> GetHashCountsByDateAsync(CancellationToken cancellationToken)
    {
        return _hashRepository.GetHashCountsByDateAsync(cancellationToken);
    }
}
