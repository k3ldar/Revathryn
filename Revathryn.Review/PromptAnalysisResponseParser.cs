using System.Text.Json;

using Revathryn.Contracts;
using Revathryn.Contracts.Review;
using Revathryn.Resources;

namespace Revathryn.Review
{
    /// <summary>
    /// Validates and converts the review engine's prompt analysis JSON. Anything that does not satisfy the
    /// contract is reported as malformed so it is never persisted as a recommendation.
    /// </summary>
    public static class PromptAnalysisResponseParser
    {
        /// <summary>Parses the model output. Never throws for malformed content.</summary>
        public static PromptAnalysisEngineResult Parse(string content)
        {
            string json = ExtractJson(content);

            if (string.IsNullOrWhiteSpace(json))
            {
                return Malformed(content, Strings.PromptAnalysisResponseEmpty);
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                if (root.ValueKind is not JsonValueKind.Object)
                {
                    return Malformed(content, Strings.PromptAnalysisResponseNotObject);
                }

                TryGetString(root, Consts.PromptAnalysisKeyAnalysisSummary, out string summary);
                List<string> causes = ReadStringList(root, Consts.PromptAnalysisKeyDisagreementCause);

                if (string.IsNullOrWhiteSpace(summary) && causes.Count == 0)
                {
                    return Malformed(content, Strings.PromptAnalysisResponseMissingSummary);
                }

                PromptAnalysisResult analysis = new()
                {
                    AnalysisSummary = summary?.Trim() ?? string.Empty,
                    DisagreementCause = causes,
                    SystemPromptRecommendation = ReadDecision(root, Consts.PromptAnalysisKeySystemPromptRecommendation),
                    ReviewPromptRecommendation = ReadDecision(root, Consts.PromptAnalysisKeyReviewPromptRecommendation),
                    AdditionalObservations = ReadStringList(root, Consts.PromptAnalysisKeyAdditionalObservations)
                };

                return new PromptAnalysisEngineResult
                {
                    Success = true,
                    RawResponse = content,
                    Analysis = analysis
                };
            }
            catch (JsonException exception)
            {
                return Malformed(content, string.Format(Strings.PromptAnalysisResponseInvalidJson, exception.Message));
            }
        }

        private static PromptRecommendationDecision ReadDecision(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out JsonElement element) || element.ValueKind is not JsonValueKind.Object)
            {
                return new PromptRecommendationDecision();
            }

            bool required = element.TryGetProperty(Consts.PromptRecommendationKeyRequired, out JsonElement requiredElement)
                && requiredElement.ValueKind is JsonValueKind.True;

            TryGetString(element, Consts.PromptRecommendationKeyExplanation, out string explanation);

            return new PromptRecommendationDecision
            {
                Required = required,
                Explanation = explanation?.Trim() ?? string.Empty
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

        private static PromptAnalysisEngineResult Malformed(string raw, string message)
        {
            return new PromptAnalysisEngineResult
            {
                Success = false,
                Malformed = true,
                RawResponse = raw ?? string.Empty,
                ErrorMessage = message
            };
        }
    }
}
