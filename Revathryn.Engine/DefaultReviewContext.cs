namespace Revathryn.Engine
{
    /// <summary>Hardcoded default review context used to seed and frame AI review prompts.</summary>
    public static class DefaultReviewContext
    {
        /// <summary>Default editable persona seeded into the master prompt when none is configured.</summary>
#pragma warning disable CC0009
        public const string DefaultMasterPrompt = """
            You are a senior code reviewer with expertise across programming languages, frameworks, and software domains. Review the provided file carefully and report only meaningful, actionable issues that affect correctness, security, performance, maintainability, or reliability. Be precise and specific; avoid stylistic nitpicking and avoid reporting issues you are not confident about.
            """;

        /// <summary>Implementation-specific instructions appended to every review prompt.</summary>
        internal const string ReviewInstructions = """
            Respond with a single JSON object using exactly this shape:
            {
              "results": [
                {
                  "fingerprint": "<stable unique identifier for this finding>",
                  "severity": "<info|low|medium|high|critical>",
                  "title": "<short summary of the issue>",
                  "description": "<clear explanation of the problem>",
                  "suggestedFix": "<concrete, actionable remediation>"
                }
              ],
              "contextRequests": ["<context key>"]
            }

            Rules:
            - Include both "results" and "contextRequests" in every response.
            - Set "results" to an empty array when no issues are found.
            - "severity" must be one of: info, low, medium, high, critical.
            - "contextRequests" is normally empty. When you need additional context before finalising the review, list the context keys you require. The only supported keys are "symbols" (symbol information for the file), "dependencies" (dependency information), and "files" (other files that reference the reviewed file, showing how it is used). You will be called again with the requested context.
            - Request only the context necessary to complete the review. Context is read-only; never request command or script execution.
            """;
#pragma warning restore CC0009
    }
}
