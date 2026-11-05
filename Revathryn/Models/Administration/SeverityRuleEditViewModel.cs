using System.ComponentModel.DataAnnotations;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>Form model for creating and editing a severity override rule.</summary>
    public sealed class SeverityRuleEditViewModel
    {
        /// <summary>Rule identifier; zero when creating.</summary>
        public long Id { get; set; }

        /// <summary>Evaluation order; lower values are evaluated first.</summary>
        [Range(Consts.MinimumSeverityRuleOrder, Consts.MaximumSeverityRuleOrder, ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.SeverityRuleOrderRangeMessage))]
        public int Order { get; set; } = Consts.DefaultSeverityRuleOrder;

        /// <summary>Display name describing the rule's intent.</summary>
        [Required(ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.NameRequiredMessage))]
        public string Name { get; set; } = string.Empty;

        /// <summary>Case-insensitive text matched against the selected finding fields.</summary>
        [Required(ErrorMessageResourceType = typeof(Revathryn.Resources.Strings), ErrorMessageResourceName = nameof(Revathryn.Resources.Strings.SeverityRuleMatchTextRequiredMessage))]
        public string MatchText { get; set; } = string.Empty;

        /// <summary>Which finding fields are searched for the match text.</summary>
        public SeverityRuleMatchField MatchField { get; set; } = SeverityRuleMatchField.Both;

        /// <summary>Optional severity filter; when set the rule only matches findings with this AI severity.</summary>
        public FindingSeverity? MatchSeverity { get; set; }

        /// <summary>How the target severity is applied to a matched finding.</summary>
        public SeverityRuleAction Action { get; set; } = SeverityRuleAction.Cap;

        /// <summary>The severity applied, or capped at, when the rule matches.</summary>
        public FindingSeverity TargetSeverity { get; set; } = FindingSeverity.Medium;

        /// <summary>Whether the rule participates in evaluation.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Creates a form model from a persisted rule.</summary>
        public static SeverityRuleEditViewModel FromRule(SeverityRule rule)
        {
            return new SeverityRuleEditViewModel
            {
                Id = rule.Id,
                Order = rule.Order,
                Name = rule.Name,
                MatchText = rule.MatchText,
                MatchField = rule.MatchField,
                MatchSeverity = rule.MatchSeverity,
                Action = rule.Action,
                TargetSeverity = rule.TargetSeverity,
                Enabled = rule.Enabled
            };
        }

        /// <summary>Converts the form model to a persisted rule.</summary>
        public SeverityRule ToRule()
        {
            return new SeverityRule
            {
                Id = Id,
                Order = Order,
                Name = Name?.Trim() ?? string.Empty,
                MatchText = MatchText?.Trim() ?? string.Empty,
                MatchField = MatchField,
                MatchSeverity = MatchSeverity,
                Action = Action,
                TargetSeverity = TargetSeverity,
                Enabled = Enabled
            };
        }
    }
}
