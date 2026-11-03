using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>The outcome of requesting an isolated prompt test run.</summary>
    public sealed class PromptTestRunStartResult
    {
        /// <summary>The test run record. Present when created or already active.</summary>
        public PromptTestRun TestRun { get; set; }

        /// <summary>True when a new test run was created.</summary>
        public bool Created { get; set; }

        /// <summary>True when an existing active test run was returned instead of creating one.</summary>
        public bool AlreadyActive { get; set; }

        /// <summary>A message safe to display.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>The rejection reason when the request could not be queued.</summary>
        public PromptTestRunRejection? Rejection { get; set; }
    }
}
