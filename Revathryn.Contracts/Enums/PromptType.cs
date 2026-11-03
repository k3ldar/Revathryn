namespace Revathryn.Contracts.Enums
{
    /// <summary>Identifies which kind of prompt a version belongs to.</summary>
    public enum PromptType
    {
        /// <summary>The global system prompt governing all detection analysis.</summary>
        System = 0,

        /// <summary>A review-specific prompt governing a particular review type.</summary>
        Review = 1
    }
}
