using System.Collections.Concurrent;
using HashProcessor.Contracts;
using HashProcessor.Database;

namespace HashProcessor.Tests.Fakes;

public class FakeHashRepository : IHashRepository
{
    private int _saveAttemptCount;
    private int _successfulSaveCount;

    public ConcurrentQueue<(string Sha1, DateTime GeneratedAtUtc)> SavedHashes { get; } = [];

    public IReadOnlyList<HashCountByDate> HashCounts { get; set; } = [];

    public CancellationToken CancellationToken { get; private set; }

    public Exception? SaveException { get; set; }

    public int FailuresBeforeSuccess { get; set; } = int.MaxValue;

    public int SaveAttemptCount => _saveAttemptCount;

    public int SuccessfulSaveCount => _successfulSaveCount;

    public Task SaveHashAsync(string sha1, DateTime generatedAtUtc, CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        var saveAttemptCount = Interlocked.Increment(ref _saveAttemptCount);
        SavedHashes.Enqueue((sha1, generatedAtUtc));

        if (SaveException is not null && saveAttemptCount <= FailuresBeforeSuccess)
        {
            throw SaveException;
        }

        Interlocked.Increment(ref _successfulSaveCount);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HashCountByDate>> GetHashCountsByDateAsync(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;

        return Task.FromResult(HashCounts);
    }
}
