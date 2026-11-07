namespace Revathryn.Contracts.Reliability
{
    /// <summary>Rechecks whether paused automated processing can now be resumed.</summary>
    public interface IProcessingPauseRevalidator
    {
        /// <summary>Resumes processing when the underlying configuration is valid again; returns true when resumed.</summary>
        Task<bool> TryResumeAsync(CancellationToken cancellationToken = default);
    }
}
