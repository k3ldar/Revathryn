using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

using Revathryn.Website.Models;
using Revathryn.Website.Services;

namespace Revathryn.Website.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class RoadmapServiceTests
    {
        [TestMethod]
        public async Task GetRoadmapAsync_ValidJson_ReturnsCategoriesAndParsesStatus()
        {
            string json = """{"categories":[{"name":"Foundation","items":[{"title":"Core architecture","description":"Description","status":"Completed","milestone":"1.0"}]}]}""";
            string root = CreateContentRoot(json);
            try
            {
                RoadmapService service = new(CreateEnvironment(root));

                RoadmapData roadmap = await service.GetRoadmapAsync();

                Assert.HasCount(1, roadmap.Categories);
                Assert.AreEqual("Foundation", roadmap.Categories[0].Name);
                Assert.HasCount(1, roadmap.Categories[0].Items);
                Assert.AreEqual(RoadmapStatus.Completed, roadmap.Categories[0].Items[0].Status);
                Assert.AreEqual("1.0", roadmap.Categories[0].Items[0].Milestone);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public async Task GetRoadmapAsync_EmptyCategories_ReturnsEmptyList()
        {
            string root = CreateContentRoot("""{"categories":[]}""");
            try
            {
                RoadmapService service = new(CreateEnvironment(root));

                RoadmapData roadmap = await service.GetRoadmapAsync();

                Assert.HasCount(0, roadmap.Categories);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public async Task GetRoadmapAsync_MissingCategoriesProperty_ReturnsEmptyList()
        {
            string root = CreateContentRoot("{}");
            try
            {
                RoadmapService service = new(CreateEnvironment(root));

                RoadmapData roadmap = await service.GetRoadmapAsync();

                Assert.HasCount(0, roadmap.Categories);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public async Task GetRoadmapAsync_MissingFile_ThrowsFileNotFoundException()
        {
            string root = Path.Combine(Path.GetTempPath(), "Revathryn.Website.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                RoadmapService service = new(CreateEnvironment(root));

                await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => service.GetRoadmapAsync());
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public async Task GetRoadmapAsync_InvalidJson_ThrowsJsonException()
        {
            string root = CreateContentRoot("not valid json");
            try
            {
                RoadmapService service = new(CreateEnvironment(root));

                await Assert.ThrowsExactlyAsync<JsonException>(() => service.GetRoadmapAsync());
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Constructor_NullEnvironment_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new RoadmapService(null));
        }

        private static FakeWebHostEnvironment CreateEnvironment(string contentRootPath)
        {
            return new FakeWebHostEnvironment { ContentRootPath = contentRootPath };
        }

        private static string CreateContentRoot(string json)
        {
            string root = Path.Combine(Path.GetTempPath(), "Revathryn.Website.Tests", Guid.NewGuid().ToString("N"));
            string dataDir = Path.Combine(root, "Data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "roadmap.json"), json);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private sealed class FakeWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; }

            public string ApplicationName { get; set; }

            public string ContentRootPath { get; set; }

            public IFileProvider ContentRootFileProvider { get; set; }

            public string WebRootPath { get; set; }

            public IFileProvider WebRootFileProvider { get; set; }
        }
    }
}
