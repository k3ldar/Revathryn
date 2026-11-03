using System.Text.Json;
using System.Text.Json.Serialization;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Review;

namespace Revathryn.Engine
{
    /// <summary>
    /// Serialises and reads the structured JSON columns stored with a <see cref="Revathryn.Contracts.Models.PromptTestRun"/>.
    /// </summary>
    public static class PromptTestRunJson
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Serialises the original detection output.</summary>
        public static string SerializeOriginalOutput(PromptTestOriginalOutput output)
        {
            return Serialize(output);
        }

        /// <summary>Serialises the complete revised detection output.</summary>
        public static string SerializeTestOutput(ReviewResponse response)
        {
            return Serialize(response);
        }

        /// <summary>Serialises the structured comparison between the two outputs.</summary>
        public static string SerializeComparison(PromptTestComparison comparison)
        {
            return Serialize(comparison);
        }

        /// <summary>Serialises the non-secret detection engine configuration used for the test.</summary>
        public static string SerializeEngineSnapshot(AIEngine engine)
        {
            if (engine is null)
            {
                return string.Empty;
            }

            return JsonSerializer.Serialize(
                new
                {
                    name = engine.Name,
                    provider = engine.Provider,
                    endpoint = engine.Endpoint,
                    model = engine.Model,
                    maxTokens = engine.MaxTokens,
                    isDefault = engine.IsDefault
                },
                Options);
        }

        /// <summary>Serialises the replayed context metadata for reproducibility tracing.</summary>
        public static string SerializeContextSnapshot(
            long? reviewContextId,
            string sha256,
            string filePath,
            string language,
            long? originalIssueId,
            string combinedPrompt)
        {
            return JsonSerializer.Serialize(
                new
                {
                    reviewContextId,
                    sha256,
                    filePath,
                    language,
                    originalIssueId,
                    combinedPrompt
                },
                Options);
        }

        /// <summary>Reads a comparison, or null when absent or unreadable.</summary>
        public static PromptTestComparison ReadComparison(string json)
        {
            return Read<PromptTestComparison>(json);
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
