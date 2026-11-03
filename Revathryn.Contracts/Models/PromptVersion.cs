using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>An immutable prompt version. Versions are never overwritten.</summary>
    public sealed class PromptVersion
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>The owning prompt identity.</summary>
        public long PromptId { get; set; }

        /// <summary>Monotonically increasing version number within the prompt identity.</summary>
        public int VersionNumber { get; set; }

        /// <summary>The exact prompt text.</summary>
        public string PromptText { get; set; } = string.Empty;

        /// <summary>Optional human-readable description of the change.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Whether the version was created manually or from an AI suggestion.</summary>
        public PromptOrigin Origin { get; set; }

        /// <summary>The optimisation that produced this version, when it originated from an AI suggestion.</summary>
        public long? SourceOptimisationId { get; set; }

        /// <summary>UTC timestamp when the version was created.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Who created the version.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>Whether this version is currently active for detection.</summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// True when this version is an editable draft that does not affect detection. Drafts become immutable
        /// historical versions once committed or activated.
        /// </summary>
        public bool IsDraft { get; set; }

        /// <summary>UTC timestamp when this version was most recently activated, or null when never active.</summary>
        public DateTime? ActivatedUtc { get; set; }
    }
}
