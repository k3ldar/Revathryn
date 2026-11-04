using System.Text.Json;

using Revathryn.Contracts;
using Revathryn.Contracts.Review;
using Revathryn.Resources;

namespace Revathryn.Review
{
    /// <summary>
    /// Validates and converts the review engine's prompt proposal JSON. A recommendation is only accepted when it
    /// contains a usable proposed prompt; anything else is discarded so the user never reviews empty suggestions.
    /// </summary>
    public static class PromptProposalResponseParser
    {
        /// <summary>Parses the model output. Never throws for malformed content.</summary>
        public static PromptProposalEngineResult Parse(string content)
        {
            string json = ExtractJson(content);

            if (string.IsNullOrWhiteSpace(json))
            {
                return Malformed(content, Strings.PromptProposalResponseEmpty);
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                if (root.ValueKind is not JsonValueKind.Object)
                {
                    return Malformed(content, Strings.PromptProposalResponseNotObject);
                }

                PromptRecommendation system = ReadRecommendation(root, Consts.PromptProposalKeySystemPrompt);
                PromptRecommendation review = ReadRecommendation(root, Consts.PromptProposalKeyReviewPrompt);

                if (system is null && review is null)
                {
                    return Malformed(content, Strings.PromptProposalResponseMissingRecommendation);
                }

                return new PromptProposalEngineResult
                {
                    Success = true,
                    RawResponse = content,
                    SystemRecommendation = system,
                    ReviewRecommendation = review
                };
            }
            catch (JsonException exception)
            {
                return Malformed(content, string.Format(Strings.PromptProposalResponseInvalidJson, exception.Message));
            }
        }

        private static PromptRecommendation ReadRecommendation(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out JsonElement element) || element.ValueKind is not JsonValueKind.Object)
            {
                return null;
            }

            if (!TryGetString(element, Consts.PromptRecommendationKeyProposedPrompt, out string proposedPrompt)
                || string.IsNullOrWhiteSpace(proposedPrompt))
            {
                return null;
            }

            TryGetString(element, Consts.PromptRecommendationKeyOriginalPrompt, out string originalPrompt);
            TryGetString(element, Consts.PromptRecommendationKeyExplanation, out string explanation);
            TryGetString(element, Consts.PromptRecommendationKeyChangesSummary, out string changesSummary);
            TryGetString(element, Consts.PromptRecommendationKeyExpectedBenefit, out string expectedBenefit);

            return new PromptRecommendation
            {
                OriginalPrompt = originalPrompt?.Trim() ?? string.Empty,
                ProposedPrompt = proposedPrompt.Trim(),
                Explanation = explanation?.Trim() ?? string.Empty,
                ChangesSummary = changesSummary?.Trim() ?? string.Empty,
                ExpectedBenefit = expectedBenefit?.Trim() ?? string.Empty,
                PotentialDrawbacks = ReadStringList(element, Consts.PromptRecommendationKeyPotentialDrawbacks)
            };
        }

        private static List<string> ReadStringList(JsonElement root, string name)
        {
            List<string> values = [];

            if (!root.TryGetProperty(name, out JsonElement element) || element.ValueKind is not JsonValueKind.Array)
            {
                return values;
            }

            foreach (JsonElement item in element.EnumerateArray())
            {
                string text = item.ValueKind is JsonValueKind.String ? item.GetString() : null;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text.Trim());
                }
            }

            return values;
        }

        private static bool TryGetString(JsonElement element, string name, out string value)
        {
            value = null;

            if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind is not JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string ExtractJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            string trimmed = content.Trim();

            if (trimmed.StartsWith(Consts.CodeFence, StringComparison.Ordinal))
            {
                int firstNewLine = trimmed.IndexOf(Consts.CharNewLine);
                int closing = trimmed.LastIndexOf(Consts.CodeFence, StringComparison.Ordinal);

                if (firstNewLine >= 0 && closing > firstNewLine)
                {
                    trimmed = trimmed[(firstNewLine + 1)..closing].Trim();
                }
            }

            return trimmed;
        }

        private static PromptProposalEngineResult Malformed(string raw, string message)
        {
            return new PromptProposalEngineResult
            {
                Success = false,
                Malformed = true,
                RawResponse = raw ?? string.Empty,
                ErrorMessage = message
            };
        }
    }
}
