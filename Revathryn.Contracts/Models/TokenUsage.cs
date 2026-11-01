namespace Revathryn.Contracts.Models
{
    /// <summary>Token usage accumulated for a single AI review engine invocation.</summary>
    public sealed class TokenUsage
    {
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
    }
}
