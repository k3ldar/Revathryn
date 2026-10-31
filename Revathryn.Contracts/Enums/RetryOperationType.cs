namespace Revathryn.Contracts.Enums
{
    /// <summary>Categories of operation that use a configurable retry policy.</summary>
    public enum RetryOperationType
    {
        /// <summary>Default policy for unclassified operations.</summary>
        Default = 0,

        /// <summary>Repository access such as reading or writing repository state.</summary>
        RepositoryAccess = 1,

        /// <summary>File system operations.</summary>
        FileSystem = 2,

        /// <summary>External service communication such as AI review calls.</summary>
        ExternalService = 3,

        /// <summary>Temporary database connectivity issues.</summary>
        Database = 4,

        /// <summary>Resource contention such as locked files or semaphores.</summary>
        ResourceContention = 5
    }
}
