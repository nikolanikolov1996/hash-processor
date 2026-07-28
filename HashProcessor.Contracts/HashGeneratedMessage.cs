namespace HashProcessor.Contracts;

public class HashGeneratedMessage
{
    public string Sha1 { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }
}