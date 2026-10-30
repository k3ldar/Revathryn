using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Revathryn.Contracts;
using Revathryn.Contracts;


namespace Revathryn.Engine
{
    /// <summary>
    /// File-backed implementation of <see cref="IManifestManager"/>.
    /// Stores manifest data as a JSON file at the path supplied on construction.
    /// Replace this class with a database-backed implementation in the future
    /// without changing any other layer.
    /// </summary>
    public class ManifestManager(string manifestPath) : IManifestManager
    {
        private readonly string _manifestPath = manifestPath;
        private readonly Manifest _manifest = Load(manifestPath);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new ManifestEntryConverter() }
        };

        /// <summary>
        /// Tells System.Text.Json to deserialise <see cref="IManifestEntry"/> as the
        /// concrete <see cref="ManifestEntry"/>. Serialisation writes whatever concrete
        /// type is held, so no type discriminator is needed in the JSON.
        /// </summary>
        private sealed class ManifestEntryConverter : JsonConverter<IManifestEntry>
        {
            public override IManifestEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => JsonSerializer.Deserialize<ManifestEntry>(ref reader, options);

            public override void Write(Utf8JsonWriter writer, IManifestEntry value, JsonSerializerOptions options)
                => JsonSerializer.Serialize(writer, (ManifestEntry)value, options);
        }

        public bool RequiresAnalysis(string filePath, int currentAnalyserVersion)
        {
            if (!_manifest.Entries.TryGetValue(filePath, out IManifestEntry existing))
            {
                return true;
            }

            // Force re-analysis when the analyser has been updated
            if (existing.AnalyserVersion < currentAnalyserVersion)
            {
                return true;
            }

            // Phase 1 — cheap timestamp pre-filter
            DateTime diskWriteTime = File.GetLastWriteTimeUtc(filePath);

            if (diskWriteTime <= existing.SourceLastWriteUtc)
            {
                return false;
            }

            // Phase 2 — timestamp differs, verify with SHA256
            string currentHash = ComputeHash(filePath);
            return !string.Equals(currentHash, existing.SourceHash, StringComparison.Ordinal);
        }

        public void RecordAnalysis(IManifestEntry entry)
        {
            _manifest.Entries[entry.SourcePath] = entry;
            Save();
        }

        public IManifestEntry GetEntry(string filePath)
        {
            _ = _manifest.Entries.TryGetValue(filePath, out IManifestEntry entry);
            return entry;
        }

        private static Manifest Load(string path)
        {
            if (!File.Exists(path))
            {
                return new Manifest();
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Manifest>(json, _jsonOptions)
                    ?? new Manifest();
            }
            catch (JsonException)
            {
                // Corrupt manifest — start fresh
                return new Manifest();
            }
        }

        private void Save()
        {
            string directory = Path.GetDirectoryName(_manifestPath);

            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(_manifest, _jsonOptions);
            File.WriteAllText(_manifestPath, json);
        }

        public static string ComputeHash(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            byte[] hashBytes = SHA256.HashData(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
