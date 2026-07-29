using HashProcessor.Contracts;

namespace HashProcessor.Database
{
    public interface IHashRepository
    {
        Task SaveHashAsync(string sha1, DateTime generatedAtUtc, CancellationToken cancellationToken);

        Task<IReadOnlyList<HashCountByDate>> GetHashCountsByDateAsync(CancellationToken cancellationToken);
    }
}
