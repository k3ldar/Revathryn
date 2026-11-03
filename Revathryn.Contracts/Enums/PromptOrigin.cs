namespace Revathryn.Contracts.Enums
{
    /// <summary>Describes how a prompt version was produced.</summary>
    public enum PromptOrigin
    {
        /// <summary>The prompt was created or edited manually by a user.</summary>
        Manual = 0,

        /// <summary>The prompt was generated from an AI suggestion.</summary>
        AISuggestion = 1
    }
}
