namespace Revathryn.Contracts.Infrastructure
{
    /// <summary>Creates automated backups of persisted data.</summary>
    public interface IBackupService
    {
        /// <summary>Creates a backup and returns the path of the created backup file.</summary>
        Task<string> CreateBackupAsync(CancellationToken cancellationToken = default);
    }
}
