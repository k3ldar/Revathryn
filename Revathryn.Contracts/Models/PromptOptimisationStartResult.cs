using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>The outcome of requesting a prompt optimisation.</summary>
    public sealed class PromptOptimisationStartResult
    {
        /// <summary>The optimisation record. Present when created or already active.</summary>
        public PromptOptimisation Optimisation { get; set; }

        /// <summary>True when a new optimisation was created.</summary>
        public bool Created { get; set; }

        /// <summary>True when an existing active optimisation was returned instead of creating one.</summary>
        public bool AlreadyActive { get; set; }

        /// <summary>A message safe to display.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>The rejection reason when the request could not be queued.</summary>
        public PromptOptimisationRejection? Rejection { get; set; }
    }
}
