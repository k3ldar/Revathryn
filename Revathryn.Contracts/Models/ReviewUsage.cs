namespace Revathryn.Contracts.Models
{
    /// <summary>Persisted usage record for a single completed AI review invocation.</summary>
    public sealed class ReviewUsage
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning repository.</summary>
        public long RepositoryId { get; set; }

        /// <summary>The reviewed file.</summary>
        public long RepoFileId { get; set; }

        /// <summary>The review type that ran.</summary>
        public long ReviewTypeId { get; set; }

        /// <summary>Identifier of the engine that served the review.</summary>
        public long EngineId { get; set; }

        /// <summary>Display name of the engine at the time of the review.</summary>
        public string EngineName { get; set; } = string.Empty;

        /// <summary>Model identifier used by the engine at the time of the review.</summary>
        public string EngineModel { get; set; } = string.Empty;

        /// <summary>Number of underlying model requests made within the invocation.</summary>
        public int AttemptCount { get; set; }

        /// <summary>Number of prompt tokens consumed.</summary>
        public int PromptTokens { get; set; }

        /// <summary>Number of completion tokens generated.</summary>
        public int CompletionTokens { get; set; }

        /// <summary>Total tokens reported by the provider.</summary>
        public int TotalTokens { get; set; }

        /// <summary>Number of prompt tokens served from the provider cache.</summary>
        public int CachedTokens { get; set; }

        /// <summary>UTC timestamp when the review completed.</summary>
        public DateTime OccurredUtc { get; set; }

        /// <summary>Git branch the review was completed against. Empty for legacy or detached HEAD records.</summary>
        public string Branch { get; set; } = string.Empty;
    }
}
