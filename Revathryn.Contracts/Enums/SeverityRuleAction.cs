namespace Revathryn.Contracts.Enums
{
    /// <summary>How a matched severity rule adjusts the AI-supplied severity.</summary>
    public enum SeverityRuleAction
    {
        /// <summary>Replace the severity with the rule's target severity.</summary>
        Set = 0,

        /// <summary>Reduce the severity so it does not exceed the rule's target severity.</summary>
        Cap = 1
    }
}
