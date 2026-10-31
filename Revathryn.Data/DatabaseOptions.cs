using static Revathryn.Contracts.Consts;

namespace Revathryn.Data
{
    /// <summary>Configuration for the SQLite persistence layer.</summary>
    public class DatabaseOptions
    {
        public DatabaseOptions()
        {
            string rootPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            BackupDirectory = Path.Combine(rootPath, DefaultProgramDataPath, DefaultBackupDirectoryName);
            ConnectionString = String.Format(DefaultDataStore, Path.Combine(rootPath, DefaultProgramDataPath, DefaultDatabaseFileName));
        }

        /// <summary>SQLite connection string. Defaults to a local file database.</summary>
        public string ConnectionString { get; set; }

        /// <summary>Directory where automated database backups are written.</summary>
        public string BackupDirectory { get; set; } = DefaultBackupDirectoryName;
    }
}
