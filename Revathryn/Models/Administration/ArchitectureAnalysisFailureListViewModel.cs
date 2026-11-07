using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>View model for the file summary failures page.</summary>
    public sealed class ArchitectureAnalysisFailureListViewModel
    {
        /// <summary>Failures, most recent first.</summary>
        public IReadOnlyList<ArchitectureAnalysisFailure> Failures { get; set; } = [];
    }
}
