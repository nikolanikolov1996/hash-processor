namespace HashProcessor.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public class MariaDbTestGroup : ICollectionFixture<MariaDbFixture>
{
    public const string Name = "MariaDB";
}
