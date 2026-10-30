using Microsoft.Data.Sqlite;

namespace Revathryn.Data
{
    /// <summary>Shared base class for SQLite repositories.</summary>
    public abstract class SqliteRepositoryBase
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        /// <summary>Initialises a new repository over the supplied connection factory.</summary>
        protected SqliteRepositoryBase(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>Opens a new connection asynchronously.</summary>
        protected Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            return _connectionFactory.OpenAsync(cancellationToken);
        }
    }
}
