using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>
    /// Reconciles the findings produced by a review against the findings already recorded for the same
    /// file and review type, producing the insert, update and auto-resolve plan for a single transaction.
    /// </summary>
    public interface IFindingReconciler
    {
        /// <summary>Plans persistence changes by correlating findings using their stable <see cref="Finding.FindingKey"/>.</summary>
        /// <param name="existingFindings">Findings already recorded for the file and review type.</param>
        /// <param name="newFindings">Findings produced by the current review.</param>
        /// <param name="reviewSha256">SHA256 of the reviewed content the resolutions are attributed to.</param>
        /// <param name="resolvedUtc">UTC timestamp to stamp onto auto-resolved findings.</param>
        FindingReconciliation Reconcile(
            IReadOnlyList<Finding> existingFindings,
            IReadOnlyList<Finding> newFindings,
            string reviewSha256,
            DateTime resolvedUtc);
    }
}
