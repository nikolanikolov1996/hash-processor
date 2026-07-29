using System.Globalization;
using MySqlConnector;
using Testcontainers.MariaDb;

namespace HashProcessor.Tests.Integration;

public class MariaDbFixture : IAsyncLifetime
{
    private readonly MariaDbContainer _container = new MariaDbBuilder("mariadb:11.8.8")
        .WithDatabase("hash_processor")
        .WithUsername("hash_processor")
        .WithPassword("hash_processor_test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ExecuteScriptAsync("001_add_hashes.sql");
        await ExecuteScriptAsync("002_add_hash_counts_by_date.sql");
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var deleteCountsCommand = connection.CreateCommand();
        deleteCountsCommand.CommandText = "DELETE FROM hash_counts_by_date";
        await deleteCountsCommand.ExecuteNonQueryAsync();

        await using var deleteHashesCommand = connection.CreateCommand();
        deleteHashesCommand.CommandText = "DELETE FROM hashes";
        await deleteHashesCommand.ExecuteNonQueryAsync();
    }

    public async Task<long> GetHashRowCountAsync()
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM hashes";

        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task ExecuteScriptAsync(string scriptName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", scriptName);
        var script = await File.ReadAllTextAsync(scriptPath);

        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = script;

        await command.ExecuteNonQueryAsync();
    }
}
