namespace Revathryn.Models.IssueValidations
{
    /// <summary>Everything the issue detail page needs to render the independent validation section.</summary>
    public sealed class IssueValidationPanelViewModel
    {
        /// <summary>Issue being validated.</summary>
        public long FindingId { get; init; }

        /// <summary>True when a new validation may be started.</summary>
        public bool CanStart { get; init; }

        /// <summary>Explains why a validation cannot be started.</summary>
        public string UnavailableReason { get; init; } = string.Empty;

        /// <summary>Validation currently pending or running, if any.</summary>
        public IssueValidationDto Active { get; init; }

        /// <summary>Validation whose result is displayed.</summary>
        public IssueValidationDto Selected { get; init; }

        /// <summary>All validations, newest first.</summary>
        public IReadOnlyList<IssueValidationDto> History { get; init; } = [];
    }
}
