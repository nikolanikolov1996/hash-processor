using MySqlConnector;

using HashProcessor.Contracts;

namespace HashProcessor.Database
{
    public class HashDatabase : IHashRepository
    {
        private readonly string _connectionString;

        public HashDatabase(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("The MariaDB connection string is required.", nameof(connectionString));
            }

            _connectionString = connectionString;
        }

        public async Task SaveHashAsync(string sha1, DateTime generatedAtUtc, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sha1) || sha1.Length != 40 || !sha1.All(Uri.IsHexDigit))
            {
                throw new ArgumentException("A SHA1 value must contain exactly 40 hexadecimal characters.", nameof(sha1));
            }

            if (generatedAtUtc.Kind != DateTimeKind.Utc || generatedAtUtc.Year < 1000)
            {
                throw new ArgumentException("The generation date must be a valid UTC date.", nameof(generatedAtUtc));
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var insertHashCommand = connection.CreateCommand();

            insertHashCommand.Transaction = transaction;
            insertHashCommand.CommandText = @"INSERT IGNORE INTO hashes (`date`, sha1)
                                              VALUES (@date, @sha1)";

            insertHashCommand.Parameters.Add("@date", MySqlDbType.DateTime).Value = generatedAtUtc;
            insertHashCommand.Parameters.Add("@sha1", MySqlDbType.VarChar, 40).Value = sha1;

            var insertedRows = await insertHashCommand.ExecuteNonQueryAsync(cancellationToken);

            if (insertedRows == 1)
            {
                await using var updateCountCommand = connection.CreateCommand();

                updateCountCommand.Transaction = transaction;
                updateCountCommand.CommandText = @"INSERT INTO hash_counts_by_date (`date`, `count`)
                                                   VALUES (DATE(@date), 1)
                                                   ON DUPLICATE KEY UPDATE `count` = `count` + 1";

                updateCountCommand.Parameters.Add("@date", MySqlDbType.DateTime).Value = generatedAtUtc;

                await updateCountCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<HashCountByDate>> GetHashCountsByDateAsync(CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();

            command.CommandText = @"SELECT `date` AS hash_date,
                                           `count` AS hash_count
                                    FROM hash_counts_by_date
                                    ORDER BY `date`";

            var hashCounts = new List<HashCountByDate>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var dateOrdinal = reader.GetOrdinal("hash_date");
            var countOrdinal = reader.GetOrdinal("hash_count");

            while (await reader.ReadAsync(cancellationToken))
            {
                hashCounts.Add(new HashCountByDate
                {
                    Date = DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                    Count = reader.GetInt64(countOrdinal)
                });
            }

            return hashCounts;
        }
    }
}
