using Microsoft.AspNetCore.Mvc;

namespace Revathryn.Website.Controllers
{
    /// <summary>Provides the about page.</summary>
    public sealed class AboutController : Controller
    {
        private const string AboutTitle = "About";
        private const string AboutDescription = "Why Revathryn exists: combining traditional code analysis with AI-assisted review to help developers understand and maintain larger software repositories.";
        private const string AboutCanonicalPath = "/about";

        /// <summary>Renders the about page.</summary>
        public IActionResult Index()
        {
            ViewData["Title"] = AboutTitle;
            ViewData["Description"] = AboutDescription;
            ViewData["CanonicalPath"] = AboutCanonicalPath;
            return View();
        }
    }
}
