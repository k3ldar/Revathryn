using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Revathryn.Contracts;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Engine
{
    /// <summary>
    /// File-backed implementation of <see cref="IManifestManager"/>.
    /// Stores one manifest per repository as a JSON file under the output
    /// directory supplied on construction. Replace this class with a
    /// database-backed implementation in the future without changing any
    /// other layer.
    /// </summary>
    public class ManifestManager : IManifestManager
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new ManifestEntryConverter() }
        };

        private readonly string _outputDirectory;
        private readonly Dictionary<string, Manifest> _manifests;

        /// <summary>Initialises a new manifest manager rooted at the supplied output directory.</summary>
        /// <param name="outputDirectory">Directory under which per-repository manifests are stored.</param>
        public ManifestManager(string outputDirectory)
        {
            _outputDirectory = outputDirectory;
            _manifests = new Dictionary<string, Manifest>(StringComparer.Ordinal);
        }

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

        public bool RequiresAnalysis(string repositoryName, string filePath, int currentAnalyserVersion)
        {
            Manifest manifest = GetManifest(repositoryName);

            if (!manifest.Entries.TryGetValue(filePath, out IManifestEntry existing))
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

        public void RecordAnalysis(string repositoryName, IManifestEntry entry)
        {
            Manifest manifest = GetManifest(repositoryName);
            manifest.Entries[entry.SourcePath] = entry;
            Save(repositoryName, manifest);
        }

        public IManifestEntry GetEntry(string repositoryName, string filePath)
        {
            Manifest manifest = GetManifest(repositoryName);
            _ = manifest.Entries.TryGetValue(filePath, out IManifestEntry entry);
            return entry;
        }

        public IReadOnlyList<IManifestEntry> GetEntries(string repositoryName)
        {
            Manifest manifest = GetManifest(repositoryName);
            return [.. manifest.Entries.Values];
        }

        private Manifest GetManifest(string repositoryName)
        {
            if (_manifests.TryGetValue(repositoryName, out Manifest manifest))
            {
                return manifest;
            }

            manifest = Load(ResolveManifestPath(repositoryName));
            _manifests[repositoryName] = manifest;
            return manifest;
        }

        private string ResolveManifestPath(string repositoryName)
        {
            string safeName = RepositoryFileNaming.GetSafeName(repositoryName);
            return Path.Combine(_outputDirectory, string.Concat(AnalysisManifestPrefix, safeName, JsonFileExtension));
        }

        private void Save(string repositoryName, Manifest manifest)
        {
            string manifestPath = ResolveManifestPath(repositoryName);

            if (!string.IsNullOrEmpty(_outputDirectory))
            {
                _ = Directory.CreateDirectory(_outputDirectory);
            }

            string json = JsonSerializer.Serialize(manifest, _jsonOptions);
            File.WriteAllText(manifestPath, json);
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

        public static string ComputeHash(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            byte[] hashBytes = SHA256.HashData(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
