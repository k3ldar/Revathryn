using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Infrastructure;

namespace Revathryn.Data
{
    /// <summary>A transaction scoped to a single SQLite connection.</summary>
    public sealed class SqliteTransactionScope : ITransactionScope
    {
        private readonly SqliteConnection _connection;
        private readonly SqliteTransaction _transaction;
        private bool _completed;

        /// <summary>Initialises a new scope over the supplied connection and transaction.</summary>
        public SqliteTransactionScope(SqliteConnection connection, SqliteTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        /// <summary>The underlying open connection.</summary>
        public SqliteConnection Connection => _connection;

        /// <summary>The underlying SQLite transaction.</summary>
        public SqliteTransaction Transaction => _transaction;

        /// <summary>Commits the transaction.</summary>
        public void Commit()
        {
            _transaction.Commit();
            _completed = true;
        }

        /// <summary>Rolls back the transaction.</summary>
        public void Rollback()
        {
            _transaction.Rollback();
            _completed = true;
        }

        /// <summary>Rolls back if not already completed, then releases resources.</summary>
        public void Dispose()
        {
            if (!_completed)
            {
                _transaction.Rollback();
            }

            _transaction.Dispose();
            _connection.Dispose();
        }
    }
}
