namespace HashProcessor.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public class RabbitMqTestGroup : ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "RabbitMQ";
}
