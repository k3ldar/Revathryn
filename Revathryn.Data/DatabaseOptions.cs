namespace Revathryn.Data
{
    /// <summary>Configuration for the SQLite persistence layer.</summary>
    public class DatabaseOptions
    {
        /// <summary>SQLite connection string. Defaults to a local file database.</summary>
        public string ConnectionString { get; set; } = "Data Source=revathryn.db";

        /// <summary>Directory where automated database backups are written.</summary>
        public string BackupDirectory { get; set; } = "backups";
    }
}
