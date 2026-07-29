using HashProcessor.Database;

namespace HashProcessor.Tests.Unit;

public class HashDatabaseValidationTests
{
    [Fact]
    public void ConstructorRejectsEmptyConnectionString()
    {
        Assert.Throws<ArgumentException>(() => new HashDatabase(string.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-sha1")]
    [InlineData("0123456789abcdef0123456789abcdef0123456g")]
    [InlineData("0123456789abcdef0123456789abcdef012345678")]
    public async Task SaveHashAsyncRejectsInvalidSha1(string sha1)
    {
        var database = new HashDatabase("Server=localhost;Database=unused;User ID=unused;Password=unused");

        await Assert.ThrowsAsync<ArgumentException>(() => database.SaveHashAsync(sha1, DateTime.UtcNow, CancellationToken.None));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task SaveHashAsyncRejectsNonUtcDate(DateTimeKind dateTimeKind)
    {
        var database = new HashDatabase("Server=localhost;Database=unused;User ID=unused;Password=unused");
        var generatedAt = DateTime.SpecifyKind(new DateTime(2026, 7, 29), dateTimeKind);

        await Assert.ThrowsAsync<ArgumentException>(() => database.SaveHashAsync("0123456789abcdef0123456789abcdef01234567",
                                                                                 generatedAt,
                                                                                 CancellationToken.None));
    }
}
