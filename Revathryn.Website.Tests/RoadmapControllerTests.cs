using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Website.Controllers;
using Revathryn.Website.Models;
using Revathryn.Website.Services;

namespace Revathryn.Website.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class RoadmapControllerTests
    {
        [TestMethod]
        public async Task Index_ReturnsViewWithRoadmapModel()
        {
            RoadmapData expected = new();
            RoadmapController controller = new(new FakeRoadmapService(expected));

            IActionResult actionResult = await controller.Index(CancellationToken.None);
            ViewResult result = (ViewResult)actionResult;

            Assert.AreSame(expected, result.Model);
        }

        [TestMethod]
        public async Task Index_SetsViewData()
        {
            RoadmapController controller = new(new FakeRoadmapService(new RoadmapData()));

            await controller.Index(CancellationToken.None);

            Assert.AreEqual("Roadmap", controller.ViewData["Title"]);
            Assert.AreEqual("/roadmap", controller.ViewData["CanonicalPath"]);
            Assert.IsFalse(string.IsNullOrWhiteSpace(controller.ViewData["Description"] as string));
        }

        [TestMethod]
        public async Task Index_EmptyCategories_ReturnsEmptyModel()
        {
            RoadmapController controller = new(new FakeRoadmapService(new RoadmapData()));

            IActionResult actionResult = await controller.Index(CancellationToken.None);
            ViewResult result = (ViewResult)actionResult;
            RoadmapData model = (RoadmapData)result.Model;

            Assert.HasCount(0, model.Categories);
        }

        private sealed class FakeRoadmapService : IRoadmapService
        {
            private readonly RoadmapData _result;

            public FakeRoadmapService(RoadmapData result)
            {
                _result = result;
            }

            public Task<RoadmapData> GetRoadmapAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_result);
            }
        }
    }
}
