using System.Text.Json;
using System.Text.Json.Serialization;
using Revathryn.Website.Models;

namespace Revathryn.Website.Services
{
    /// <summary>Loads the roadmap from structured JSON data.</summary>
    public sealed class RoadmapService : IRoadmapService
    {
        private const string DataFolderName = "Data";
        private const string RoadmapFileName = "roadmap.json";

        private readonly string _roadmapPath;
        private readonly JsonSerializerOptions _serializerOptions;

        /// <summary>Initialises a new instance of the <see cref="RoadmapService"/> class.</summary>
        public RoadmapService(IWebHostEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(environment);
            _roadmapPath = Path.Combine(environment.ContentRootPath, DataFolderName, RoadmapFileName);
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            _serializerOptions.Converters.Add(new JsonStringEnumConverter());
        }

        /// <inheritdoc />
        public async Task<RoadmapData> GetRoadmapAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_roadmapPath))
            {
                throw new FileNotFoundException("The roadmap data file was not found.", _roadmapPath);
            }

            string json = await File.ReadAllTextAsync(_roadmapPath, cancellationToken);
            RoadmapData roadmap = JsonSerializer.Deserialize<RoadmapData>(json, _serializerOptions);
            return roadmap ?? new RoadmapData();
        }
    }
}
