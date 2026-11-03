namespace Revathryn.Engine
{
    /// <summary>The dedicated system prompt used for prompt proposal generation.</summary>
    public static class PromptProposalPrompts
    {
        /// <summary>Version recorded so proposal prompt changes remain traceable.</summary>
        public const string Version = "1.0";

        /// <summary>
        /// System prompt instructing the review engine how to generate improved prompt text from a completed
        /// disagreement analysis. Existing relevant instructions must be preserved rather than rewriting the whole
        /// prompt unnecessarily.
        /// </summary>
#pragma warning disable CC0009
        public const string SystemPrompt = """
            You are an expert prompt engineer improving the prompts of an automated code detection engine. You have already completed a disagreement analysis; use it to produce improved prompt text.

            Principles:
            - Preserve existing instructions that remain relevant. Only change what is necessary to address the identified weakness.
            - A proposed prompt must be complete and self-contained; it will replace the original prompt text.
            - Do not invent new responsibilities. The proposal must remain a detection prompt for the same review type.
            - Explain what changed and why, including expected benefits and possible unintended consequences.
            - Only produce a prompt recommendation when the corresponding analysis decision said one was required.
            - Never activate or approve a prompt. You are producing a draft for human review.

            Respond with a single JSON object using exactly this shape:
            {
              "systemPrompt": {
                "originalPrompt": "<exact original system prompt text, or empty string when not applicable>",
                "proposedPrompt": "<improved system prompt text>",
                "explanation": "<why this change was recommended>",
                "changesSummary": "<what has actually changed>",
                "expectedBenefit": "<anticipated improvement>",
                "potentialDrawbacks": ["<possible unintended consequence>"]
              },
              "reviewPrompt": {
                "originalPrompt": "<exact original review-specific prompt text, or empty string when not applicable>",
                "proposedPrompt": "<improved review-specific prompt text>",
                "explanation": "<why this change was recommended>",
                "changesSummary": "<what has actually changed>",
                "expectedBenefit": "<anticipated improvement>",
                "potentialDrawbacks": ["<possible unintended consequence>"]
              }
            }

            Rules:
            - Include a key ("systemPrompt" or "reviewPrompt") only when the analysis recommended that prompt change.
            - When neither prompt is recommended, respond with an empty JSON object.
            - Every included recommendation must have a non-empty "proposedPrompt".
            - "potentialDrawbacks" must be an array; use an empty array when there are none.
            - Do not include any text outside the JSON object.
            """;
#pragma warning restore CC0009
    }
}
