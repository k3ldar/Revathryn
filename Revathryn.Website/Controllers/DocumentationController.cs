using Microsoft.AspNetCore.Mvc;

namespace Revathryn.Website.Controllers
{
    /// <summary>Provides the documentation page.</summary>
    public sealed class DocumentationController : Controller
    {
        private const string DocumentationTitle = "Documentation";
        private const string DocumentationDescription = "Documentation for Revathryn: getting started, installation, configuration, repository setup, review engines and issue management.";
        private const string DocumentationCanonicalPath = "/documentation";

        /// <summary>Renders the documentation page.</summary>
        public IActionResult Index()
        {
            ViewData["Title"] = DocumentationTitle;
            ViewData["Description"] = DocumentationDescription;
            ViewData["CanonicalPath"] = DocumentationCanonicalPath;
            return View();
        }
    }
}
