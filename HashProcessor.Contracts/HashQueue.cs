namespace HashProcessor.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "This type describes a RabbitMQ queue contract, not a collection.")]
public static class HashQueue
{
    public const string Name = "hashes";
    public const string DeadLetterExchangeName = "hashes.failed";
    public const string DeadLetterQueueName = "hashes.failed";
    public const string DeadLetterRoutingKey = "hashes.failed";
    public const string RetryHeaderName = "x-hash-processor-retry-count";
    public const int MaxRetryCount = 3;
}
