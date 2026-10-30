namespace Revathryn.Contracts.Orchestration
{
    /// <summary>Schedules review operations for repositories and files.</summary>
    public interface IReviewScheduler
    {
        /// <summary>Enqueues review work for a whole repository based on its review types.</summary>
        Task<ReviewScheduleResult> ScheduleRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Enqueues review work for a single file based on applicable review types.</summary>
        Task<ReviewScheduleResult> ScheduleFileAsync(long repoFileId, CancellationToken cancellationToken = default);
    }
}
