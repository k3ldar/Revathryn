using System.Text.Json;
using System.Text.Json.Serialization;

using Revathryn.Contracts.Models;

namespace Revathryn.Engine
{
    /// <summary>Serialises and reads the structured JSON columns stored with an <see cref="IssueValidation"/>.</summary>
    public static class IssueValidationJson
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Serialises a value, or returns an empty string for null.</summary>
        public static string Serialize<T>(T value)
        {
            return value is null ? string.Empty : JsonSerializer.Serialize(value, Options);
        }

        /// <summary>Builds the non-secret engine configuration snapshot stored with a validation.</summary>
        public static string SerializeEngineSnapshot(ReviewEngineSettings settings)
        {
            if (settings is null)
            {
                return string.Empty;
            }

            return JsonSerializer.Serialize(
                new
                {
                    provider = settings.Provider,
                    endpoint = settings.Endpoint,
                    model = settings.Model,
                    temperature = settings.Temperature,
                    reasoningEffort = settings.ReasoningEffort,
                    maxTokens = settings.MaxTokens,
                    additionalOptions = settings.AdditionalOptions
                },
                Options);
        }

        /// <summary>Reads the validation evidence.</summary>
        public static IReadOnlyList<IssueValidationEvidence> ReadEvidence(string json)
        {
            return Read<List<IssueValidationEvidence>>(json) ?? [];
        }

        /// <summary>Reads a list of strings such as suggestions or limitations.</summary>
        public static IReadOnlyList<string> ReadStrings(string json)
        {
            return Read<List<string>>(json) ?? [];
        }

        /// <summary>Reads the context reference, or null when absent or unreadable.</summary>
        public static IssueValidationContextReference ReadContextReference(string json)
        {
            return Read<IssueValidationContextReference>(json);
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
