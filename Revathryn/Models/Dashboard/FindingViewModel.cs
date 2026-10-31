using Revathryn.Contracts.Models;

namespace Revathryn.Models.Dashboard
{
    /// <summary>Pairs a finding with display context for dashboard views.</summary>
    public sealed class FindingViewModel
    {
        /// <summary>The finding.</summary>
        public Finding Finding { get; set; }

        /// <summary>Display name of the review type that produced the finding.</summary>
        public string ReviewTypeName { get; set; } = string.Empty;

        /// <summary>Repository-relative path of the associated file.</summary>
        public string FileRelativePath { get; set; } = string.Empty;
    }
}
