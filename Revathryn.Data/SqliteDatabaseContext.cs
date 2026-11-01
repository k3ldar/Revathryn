using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Infrastructure;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IDatabaseContext"/>.</summary>
    public sealed class SqliteDatabaseContext : IDatabaseContext
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        /// <summary>Initialises a new context over the supplied connection factory.</summary>
        public SqliteDatabaseContext(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>Begins a new transaction over a dedicated connection.</summary>
        public ITransactionScope BeginTransaction()
        {
            SqliteConnection connection = _connectionFactory.Open();
            SqliteTransaction transaction = connection.BeginTransaction();
            return new SqliteTransactionScope(connection, transaction);
        }

        /// <summary>Verifies that the database is reachable and queryable.</summary>
        public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using SqliteConnection connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT 1;";
                await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (SqliteException)
            {
                return false;
            }
        }
    }
}
