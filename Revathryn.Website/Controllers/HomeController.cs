using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Website.Models;

namespace Revathryn.Website.Controllers
{
    /// <summary>Provides the home and error pages.</summary>
    public sealed class HomeController : Controller
    {
        private const string ErrorTitle = "Something went wrong";

        private readonly WebsiteSettings _settings;

        /// <summary>Initialises a new instance of the <see cref="HomeController"/> class.</summary>
        public HomeController(WebsiteSettings settings)
        {
            _settings = settings;
        }

        /// <summary>Renders the home page.</summary>
        public IActionResult Index()
        {
            ViewData["Description"] = _settings.WebsiteDescription;
            ViewData["CanonicalPath"] = "/";
            return View();
        }

        /// <summary>Renders the error page.</summary>
        public IActionResult Error(int? statusCode)
        {
            ErrorViewModel model = new()
            {
                StatusCode = statusCode ?? StatusCodes.Status500InternalServerError,
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            };

            ViewData["Title"] = ErrorTitle;
            return View(model);
        }
    }
}
