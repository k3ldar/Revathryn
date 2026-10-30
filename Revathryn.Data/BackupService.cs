using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Infrastructure;

namespace Revathryn.Data
{
    /// <summary>Creates automated backups of the SQLite database.</summary>
    public sealed class BackupService : IBackupService
    {
        private readonly SqliteConnectionFactory _connectionFactory;
        private readonly DatabaseOptions _options;

        /// <summary>Initialises a new backup service.</summary>
        public BackupService(SqliteConnectionFactory connectionFactory, DatabaseOptions options)
        {
            _connectionFactory = connectionFactory;
            _options = options;
        }

        /// <summary>Creates a backup and returns the path of the created backup file.</summary>
        public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(_options.BackupDirectory);

            string fileName = $"revathryn-backup-{DateTime.UtcNow:yyyyMMddHHmmssfff}.db";
            string destinationPath = Path.Combine(_options.BackupDirectory, fileName);

            using SqliteConnection source = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteConnection destination = new($"Data Source={destinationPath}");
            await destination.OpenAsync(cancellationToken).ConfigureAwait(false);

            source.BackupDatabase(destination);

            return destinationPath;
        }
    }
}
