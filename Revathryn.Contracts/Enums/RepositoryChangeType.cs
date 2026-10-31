namespace Revathryn.Contracts.Enums
{
    /// <summary>Categories describing how a repository file changed between reconciliation passes.</summary>
    public enum RepositoryChangeType
    {
        /// <summary>The file was discovered for the first time.</summary>
        Added = 0,

        /// <summary>The file already existed and its content fingerprint changed.</summary>
        Modified = 1,

        /// <summary>The file was previously present but is no longer discoverable.</summary>
        Removed = 2,

        /// <summary>The file already existed and its content fingerprint was unchanged.</summary>
        Unchanged = 3,

        /// <summary>The file was reliably detected as renamed or moved from another path.</summary>
        Renamed = 4
    }
}
