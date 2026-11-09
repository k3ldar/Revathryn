using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Orchestration
{
    /// <summary>Schedules review operations for repositories and files.</summary>
    public interface IReviewScheduler
    {
        /// <summary>Enqueues review work for a whole repository based on its review types.</summary>
        Task<ReviewScheduleResult> ScheduleRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default);

        /// <summary>Enqueues review work for a single file based on applicable review types.</summary>
        Task<ReviewScheduleResult> ScheduleFileAsync(long repoFileId, CancellationToken cancellationToken = default);

        /// <summary>Requeues a single file for a specific review type, forcing re-review even when its analysis is current.</summary>
        Task<ReviewScheduleResult> RequeueFileAsync(long repoFileId, long reviewTypeId, CancellationToken cancellationToken = default);

        /// <summary>Returns the enabled review types that apply to the file, respecting disabled links.</summary>
        Task<IReadOnlyList<ReviewType>> GetApplicableReviewTypesAsync(RepoFile file, CancellationToken cancellationToken = default);

        /// <summary>Determines whether the review type already has a current completed analysis for the file.</summary>
        Task<bool> IsCurrentAsync(RepoFile file, ReviewType reviewType, CancellationToken cancellationToken = default);
    }
}
