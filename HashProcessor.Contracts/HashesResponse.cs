namespace HashProcessor.Contracts;

public class HashesResponse
{
    public IReadOnlyList<HashCountByDate> Hashes { get; set; } = [];
}
