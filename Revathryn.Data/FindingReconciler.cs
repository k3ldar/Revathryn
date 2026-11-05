using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Data
{
    /// <summary>
    /// Reconciles the findings produced by a review against the findings already recorded for the same
    /// file and review type, so that issues that disappear are auto-resolved and issues that reappear
    /// retain their human triage.
    /// </summary>
    public sealed class FindingReconciler : IFindingReconciler
    {
        /// <summary>Plans persistence changes by correlating findings using their stable <see cref="Finding.FindingKey"/>.</summary>
        public FindingReconciliation Reconcile(
            IReadOnlyList<Finding> existingFindings,
            IReadOnlyList<Finding> newFindings,
            string reviewSha256,
            DateTime resolvedUtc)
        {
            ArgumentNullException.ThrowIfNull(existingFindings);
            ArgumentNullException.ThrowIfNull(newFindings);

            Dictionary<string, Finding> existingByKey = new(StringComparer.Ordinal);
            foreach (Finding existing in existingFindings)
            {
                if (string.IsNullOrEmpty(existing.FindingKey))
                {
                    continue;
                }

                existingByKey.TryAdd(existing.FindingKey, existing);
            }

            List<Finding> findingsToInsert = [];
            List<Finding> findingsToUpdate = [];
            HashSet<string> matchedKeys = new(StringComparer.Ordinal);

            foreach (Finding candidate in newFindings)
            {
                if (string.IsNullOrEmpty(candidate.FindingKey) || !existingByKey.TryGetValue(candidate.FindingKey, out Finding existing))
                {
                    findingsToInsert.Add(candidate);
                    continue;
                }

                matchedKeys.Add(candidate.FindingKey);
                findingsToUpdate.Add(Refresh(existing, candidate));
            }

            List<Finding> findingsToResolve = [];
            foreach (Finding existing in existingFindings)
            {
                if (!ShouldResolve(existing, matchedKeys))
                {
                    continue;
                }

                Resolve(existing, reviewSha256, resolvedUtc);
                findingsToResolve.Add(existing);
            }

            return new FindingReconciliation
            {
                FindingsToInsert = findingsToInsert,
                FindingsToUpdate = findingsToUpdate,
                FindingsToResolve = findingsToResolve
            };
        }

        private static bool ShouldResolve(Finding existing, HashSet<string> matchedKeys)
        {
            if (string.IsNullOrEmpty(existing.FindingKey))
            {
                return false;
            }

            if (matchedKeys.Contains(existing.FindingKey))
            {
                return false;
            }

            if (existing.Category != FindingCategory.CodeIssue)
            {
                return false;
            }

            return existing.Status is FindingStatus.New or FindingStatus.Reviewed;
        }

        private static Finding Refresh(Finding existing, Finding candidate)
        {
            if (existing.Status == FindingStatus.Resolved && existing.ResolutionOrigin == FindingResolutionOrigin.Auto)
            {
                existing.Status = FindingStatus.New;
                existing.ResolutionOrigin = FindingResolutionOrigin.None;
                existing.ResolvedUtc = null;
                existing.ResolvedReviewSha256 = string.Empty;
            }

            existing.Fingerprint = candidate.Fingerprint;
            existing.Severity = candidate.Severity;
            existing.Title = candidate.Title;
            existing.Description = candidate.Description;
            existing.SuggestedFix = candidate.SuggestedFix;
            existing.LineStart = candidate.LineStart;
            existing.LineEnd = candidate.LineEnd;
            existing.Category = candidate.Category;
            existing.LastModifiedDate = candidate.LastModifiedDate;

            return existing;
        }

        private static void Resolve(Finding finding, string reviewSha256, DateTime resolvedUtc)
        {
            finding.Status = FindingStatus.Resolved;
            finding.ResolutionOrigin = FindingResolutionOrigin.Auto;
            finding.ResolvedUtc = resolvedUtc;
            finding.ResolvedReviewSha256 = reviewSha256 ?? string.Empty;
            finding.LastModifiedDate = resolvedUtc;
        }
    }
}
