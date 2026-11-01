using Revathryn.Contracts;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Data
{
    /// <summary>Configuration for the SQLite persistence layer.</summary>
    public class DatabaseOptions
    {
        public DatabaseOptions(RevathrynSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings, nameof(settings));

            string dbPath = Path.Combine(settings.BaseSettingsPath, DatabasePath);

            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }

            BackupDirectory = Path.Combine(settings.BaseSettingsPath, DefaultBackupDirectoryName);
            ConnectionString = String.Format(DefaultDataStore, Path.Combine(dbPath, DefaultDatabaseFileName));
        }

        /// <summary>SQLite connection string. Defaults to a local file database.</summary>
        public string ConnectionString { get; set; }

        /// <summary>Directory where automated database backups are written.</summary>
        public string BackupDirectory { get; set; } = DefaultBackupDirectoryName;
    }
}
