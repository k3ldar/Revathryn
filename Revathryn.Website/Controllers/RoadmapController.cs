using Microsoft.AspNetCore.Mvc;
using Revathryn.Website.Models;
using Revathryn.Website.Services;

namespace Revathryn.Website.Controllers
{
    /// <summary>Provides the project roadmap page.</summary>
    public sealed class RoadmapController : Controller
    {
        private const string RoadmapTitle = "Roadmap";
        private const string RoadmapDescription = "The Revathryn development roadmap: current status, priorities and future direction across foundation, analysis, issue management, validation and platform operations.";
        private const string RoadmapCanonicalPath = "/roadmap";

        private readonly IRoadmapService _roadmapService;

        /// <summary>Initialises a new instance of the <see cref="RoadmapController"/> class.</summary>
        public RoadmapController(IRoadmapService roadmapService)
        {
            _roadmapService = roadmapService;
        }

        /// <summary>Renders the roadmap page.</summary>
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            RoadmapData roadmap = await _roadmapService.GetRoadmapAsync(cancellationToken);

            ViewData["Title"] = RoadmapTitle;
            ViewData["Description"] = RoadmapDescription;
            ViewData["CanonicalPath"] = RoadmapCanonicalPath;
            return View(roadmap);
        }
    }
}
