using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>The outcome of requesting an independent validation.</summary>
    public sealed class IssueValidationRequestResult
    {
        /// <summary>The validation that was created, or the one already in progress.</summary>
        public IssueValidation Validation { get; set; }

        /// <summary>True when a new validation was queued; false when a request was rejected or one was already active.</summary>
        public bool Created { get; set; }

        /// <summary>True when an active validation already existed and no duplicate was created.</summary>
        public bool AlreadyActive { get; set; }

        /// <summary>Why the request was rejected, or null when accepted.</summary>
        public IssueValidationRejection? Rejection { get; set; }

        /// <summary>A message safe to display when rejected.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
