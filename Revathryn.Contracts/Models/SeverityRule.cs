using Revathryn.Contracts.Enums;

namespace Revathryn.Contracts.Models
{
    /// <summary>
    /// A configurable rule that overrides the severity assigned by the AI review engine.
    /// Rules are evaluated in ascending <see cref="Order"/> and the first match wins.
    /// </summary>
    public sealed class SeverityRule
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Evaluation order; lower values are evaluated first.</summary>
        public int Order { get; set; }

        /// <summary>Display name describing the rule's intent.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Case-insensitive text matched against the selected finding fields.</summary>
        public string MatchText { get; set; } = string.Empty;

        /// <summary>Which finding fields are searched for <see cref="MatchText"/>.</summary>
        public SeverityRuleMatchField MatchField { get; set; } = SeverityRuleMatchField.Both;

        /// <summary>Optional severity filter; when set the rule only matches findings with this AI severity.</summary>
        public FindingSeverity? MatchSeverity { get; set; }

        /// <summary>How the target severity is applied to a matched finding.</summary>
        public SeverityRuleAction Action { get; set; } = SeverityRuleAction.Cap;

        /// <summary>The severity applied, or capped at, when the rule matches.</summary>
        public FindingSeverity TargetSeverity { get; set; } = FindingSeverity.Medium;

        /// <summary>Whether the rule participates in evaluation.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>UTC timestamp when the rule was created.</summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>UTC timestamp when the rule was last modified.</summary>
        public DateTime LastModifiedDate { get; set; }
    }
}
