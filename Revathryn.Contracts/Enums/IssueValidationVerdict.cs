namespace Revathryn.Contracts.Enums
{
    /// <summary>The conclusion reached by the review engine when independently validating a finding.</summary>
    public enum IssueValidationVerdict
    {
        /// <summary>The original finding is valid.</summary>
        Agreed = 0,

        /// <summary>The original finding is invalid or incorrectly identified.</summary>
        Disagreed = 1,

        /// <summary>The finding is valid in principle but needs correction.</summary>
        PartiallyAgreed = 2,

        /// <summary>There was insufficient evidence to reach a reliable conclusion.</summary>
        Inconclusive = 3
    }
}
