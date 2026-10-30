using Microsoft.Data.Sqlite;

namespace Revathryn.Data
{
    /// <summary>Opens SQLite connections using the configured connection string.</summary>
    public sealed class SqliteConnectionFactory
    {
        private readonly DatabaseOptions _options;

        /// <summary>Initialises a new factory.</summary>
        public SqliteConnectionFactory(DatabaseOptions options)
        {
            _options = options;
        }

        /// <summary>Opens and returns a new connection with foreign keys enabled.</summary>
        public SqliteConnection Open()
        {
            SqliteConnection connection = new(_options.ConnectionString);
            connection.Open();
            EnableForeignKeys(connection);
            return connection;
        }

        /// <summary>Opens and returns a new connection asynchronously with foreign keys enabled.</summary>
        public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            SqliteConnection connection = new(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            EnableForeignKeys(connection);
            return connection;
        }

        private static void EnableForeignKeys(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }
    }
}
