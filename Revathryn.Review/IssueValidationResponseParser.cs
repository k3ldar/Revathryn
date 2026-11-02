using System.Text.Json;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Review;
using Revathryn.Resources;

namespace Revathryn.Review
{
    /// <summary>
    /// Validates and converts the review engine's JSON output. Anything that does not satisfy the contract is
    /// reported as malformed so it is never persisted as a verdict.
    /// </summary>
    public static class IssueValidationResponseParser
    {
        private const double MaximumNormalisedConfidence = 1.0;
        private const double MaximumPercentConfidence = 100.0;
        private const double PercentDivisor = 100.0;

        /// <summary>Parses the model output. Never throws for malformed content.</summary>
        public static IssueValidationEngineResult Parse(string content, IReadOnlyDictionary<string, int> suppliedSources)
        {
            string json = ExtractJson(content);

            if (string.IsNullOrWhiteSpace(json))
            {
                return Malformed(content, Strings.ValidationResponseEmpty);
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                if (root.ValueKind is not JsonValueKind.Object)
                {
                    return Malformed(content, Strings.ValidationResponseNotObject);
                }

                return ParseObject(root, content, suppliedSources ?? new Dictionary<string, int>());
            }
            catch (JsonException exception)
            {
                return Malformed(content, string.Format(Strings.ValidationResponseInvalidJson, exception.Message));
            }
        }

        /// <summary>Converts a verdict string, ignoring case, spaces, underscores and hyphens.</summary>
        public static bool TryParseVerdict(string value, out IssueValidationVerdict verdict)
        {
            verdict = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalised = value
                .Replace(Consts.SpaceSeparator, string.Empty, StringComparison.Ordinal)
                .Replace(Consts.UnderscoreText, string.Empty, StringComparison.Ordinal)
                .Replace(Consts.HyphenText, string.Empty, StringComparison.Ordinal);

            foreach (IssueValidationVerdict candidate in Enum.GetValues<IssueValidationVerdict>())
            {
                if (string.Equals(candidate.ToString(), normalised, StringComparison.OrdinalIgnoreCase))
                {
                    verdict = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Normalises confidence to 0 - 1. Values from above 1 to 100 are treated as percentages; anything else is discarded.</summary>
        public static double? NormaliseConfidence(double? value)
        {
            if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value < 0)
            {
                return null;
            }

            if (value.Value <= MaximumNormalisedConfidence)
            {
                return value.Value;
            }

            return value.Value <= MaximumPercentConfidence ? value.Value / PercentDivisor : null;
        }

        private static IssueValidationEngineResult ParseObject(JsonElement root, string raw, IReadOnlyDictionary<string, int> suppliedSources)
        {
            if (!TryGetString(root, Consts.ValidationKeyVerdict, out string verdictText)
                || !TryParseVerdict(verdictText, out IssueValidationVerdict verdict))
            {
                return Malformed(raw, string.Format(Strings.ValidationResponseVerdictInvalid, verdictText));
            }

            TryGetString(root, Consts.ValidationKeySummary, out string summary);
            TryGetString(root, Consts.ValidationKeyReasoning, out string reasoning);

            if (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(reasoning))
            {
                return Malformed(raw, Strings.ValidationResponseMissingExplanation);
            }

            IssueValidationEngineResult result = new()
            {
                Success = true,
                RawResponse = raw,
                Verdict = verdict,
                Summary = summary?.Trim() ?? string.Empty,
                Reasoning = reasoning?.Trim() ?? string.Empty,
                Confidence = ReadConfidence(root)
            };

            ReadSeverity(root, result);
            result.Suggestions = ReadStringList(root, Consts.ValidationKeySuggestions);
            result.Limitations = ReadStringList(root, Consts.ValidationKeyLimitations);
            ReadEvidence(root, suppliedSources, result);
            return result;
        }

        private static double? ReadConfidence(JsonElement root)
        {
            if (!root.TryGetProperty(Consts.ValidationKeyConfidence, out JsonElement element))
            {
                return null;
            }

            if (element.ValueKind is JsonValueKind.Number && element.TryGetDouble(out double number))
            {
                return NormaliseConfidence(number);
            }

            if (element.ValueKind is JsonValueKind.String
                && double.TryParse(
                    element.GetString()?.TrimEnd(Consts.PercentSign),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double parsed))
            {
                return NormaliseConfidence(parsed);
            }

            return null;
        }

        private static void ReadSeverity(JsonElement root, IssueValidationEngineResult result)
        {
            if (!root.TryGetProperty(Consts.ValidationKeySeverity, out JsonElement severity) || severity.ValueKind is not JsonValueKind.Object)
            {
                return;
            }

            if (TryGetString(severity, Consts.ValidationKeyRecommended, out string recommended)
                && Enum.TryParse(recommended.Trim(), true, out FindingSeverity parsed)
                && Enum.IsDefined(parsed))
            {
                result.RecommendedSeverity = parsed;
            }

            if (TryGetString(severity, Consts.ValidationKeyReasoning, out string severityReasoning))
            {
                result.SeverityReasoning = severityReasoning.Trim();
            }
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

        private static void ReadEvidence(JsonElement root, IReadOnlyDictionary<string, int> suppliedSources, IssueValidationEngineResult result)
        {
            if (!root.TryGetProperty(Consts.ValidationKeyEvidence, out JsonElement element) || element.ValueKind is not JsonValueKind.Array)
            {
                return;
            }

            Dictionary<string, int> lookup = new(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, int> source in suppliedSources)
            {
                lookup[NormalisePath(source.Key)] = source.Value;
            }

            int discarded = 0;

            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueKind is not JsonValueKind.Object || !TryGetString(item, Consts.ValidationKeyFile, out string file))
                {
                    discarded++;
                    continue;
                }

                int start = ReadInt(item, Consts.ValidationKeyLineStart);
                int end = ReadInt(item, Consts.ValidationKeyLineEnd);

                if (end < start)
                {
                    end = start;
                }

                if (!lookup.TryGetValue(NormalisePath(file), out int lineCount) || start < 1 || end > lineCount)
                {
                    discarded++;
                    continue;
                }

                TryGetString(item, Consts.ValidationKeyExplanation, out string explanation);

                result.Evidence.Add(new IssueValidationEvidence
                {
                    File = NormalisePath(file),
                    LineStart = start,
                    LineEnd = end,
                    Explanation = explanation?.Trim() ?? string.Empty
                });
            }

            if (discarded > 0)
            {
                result.Limitations.Add(string.Format(Strings.ValidationEvidenceDiscarded, discarded));
            }
        }

        private static int ReadInt(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value)
                && value.ValueKind is JsonValueKind.Number
                && value.TryGetInt32(out int number)
                    ? number
                    : 0;
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

        private static string NormalisePath(string path)
        {
            string normalised = path.Trim().Replace(Consts.Backslash, Consts.CharForwardSlash);

            while (normalised.StartsWith(Consts.CurrentDirectoryPrefix, StringComparison.Ordinal))
            {
                normalised = normalised[Consts.CurrentDirectoryPrefix.Length..];
            }

            return normalised;
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
                int firstNewLine = trimmed.IndexOf(Consts.NewLine);
                int closing = trimmed.LastIndexOf(Consts.CodeFence, StringComparison.Ordinal);

                if (firstNewLine >= 0 && closing > firstNewLine)
                {
                    trimmed = trimmed[(firstNewLine + 1)..closing].Trim();
                }
            }

            return trimmed;
        }

        private static IssueValidationEngineResult Malformed(string raw, string message)
        {
            return new IssueValidationEngineResult
            {
                Success = false,
                Malformed = true,
                RawResponse = raw ?? string.Empty,
                ErrorMessage = message
            };
        }
    }
}
