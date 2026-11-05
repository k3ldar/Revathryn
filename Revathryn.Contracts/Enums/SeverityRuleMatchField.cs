namespace Revathryn.Contracts.Enums
{
    /// <summary>Which finding fields a severity rule searches for its match text.</summary>
    public enum SeverityRuleMatchField
    {
        /// <summary>Match against the finding title only.</summary>
        Title = 0,

        /// <summary>Match against the finding description only.</summary>
        Description = 1,

        /// <summary>Match against both the title and description.</summary>
        Both = 2
    }
}
