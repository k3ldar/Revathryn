using Revathryn.Contracts.Models;

namespace Revathryn.Models.Api
{
    /// <summary>Aggregated information Revathryn holds about a single repository file.</summary>
    public sealed class FileDetailsResponse
    {
        /// <summary>The repository containing the file.</summary>
        public Repository Repository { get; init; } = new();

        /// <summary>The file.</summary>
        public RepoFile File { get; init; } = new();

        /// <summary>Cached AI architecture overview, when one has been produced.</summary>
        public RepoFileArchitecture Architecture { get; init; }

        /// <summary>Review coverage state per applicable review type.</summary>
        public IReadOnlyList<FileReviewStatus> Reviews { get; init; } = [];

        /// <summary>Issues recorded against the file.</summary>
        public IReadOnlyList<FileIssueDetails> Issues { get; init; } = [];

        /// <summary>Code-analysis symbols defined in the file.</summary>
        public IReadOnlyList<IndexedSymbol> Symbols { get; init; } = [];

        /// <summary>Framework dependencies of the file (System.* and Microsoft.*), deduplicated.</summary>
        public IReadOnlyList<string> SystemDependencies { get; init; } = [];

        /// <summary>Non-framework dependencies of the file, deduplicated.</summary>
        public IReadOnlyList<string> OtherDependencies { get; init; } = [];

        /// <summary>UTC timestamp when the response was assembled.</summary>
        public DateTime ExportedUtc { get; init; }
    }
}
