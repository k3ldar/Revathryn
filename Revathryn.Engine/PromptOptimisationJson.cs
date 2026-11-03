using System.Text.Json;
using System.Text.Json.Serialization;

using Revathryn.Contracts.Review;

namespace Revathryn.Engine
{
    /// <summary>
    /// Serialises and reads the structured JSON columns stored with a <see cref="PromptOptimisation"/>.
    /// </summary>
    public static class PromptOptimisationJson
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Serialises the disagreement analysis.</summary>
        public static string SerializeAnalysis(PromptAnalysisResult analysis)
        {
            return Serialize(analysis);
        }

        /// <summary>Reads the disagreement analysis, or an empty analysis when absent or unreadable.</summary>
        public static PromptAnalysisResult ReadAnalysis(string json)
        {
            return Read<PromptAnalysisResult>(json) ?? new PromptAnalysisResult();
        }

        /// <summary>Serialises a prompt recommendation.</summary>
        public static string SerializeRecommendation(PromptRecommendation recommendation)
        {
            return Serialize(recommendation);
        }

        /// <summary>Reads a prompt recommendation, or null when absent or unreadable.</summary>
        public static PromptRecommendation ReadRecommendation(string json)
        {
            return Read<PromptRecommendation>(json);
        }

        private static string Serialize<T>(T value)
        {
            return value is null ? string.Empty : JsonSerializer.Serialize(value, Options);
        }

        private static T Read<T>(string json)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json, Options);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
