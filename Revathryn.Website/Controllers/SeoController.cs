using System.Text;
using System.Xml.Linq;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Website.Models;

namespace Revathryn.Website.Controllers
{
    /// <summary>Serves search engine discovery files.</summary>
    public sealed class SeoController : Controller
    {
        private const string RobotsContentType = "text/plain";
        private const string SitemapContentType = "application/xml";
        private const string RobotsTemplate = "User-agent: *\nAllow: /\n\nSitemap: {0}/sitemap.xml\n";
        private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

        private static readonly string[] SitemapPaths = ["/", "/roadmap", "/about", "/documentation"];

        private readonly WebsiteSettings _settings;

        /// <summary>Initialises a new instance of the <see cref="SeoController"/> class.</summary>
        public SeoController(WebsiteSettings settings)
        {
            _settings = settings;
        }

        /// <summary>Serves robots.txt.</summary>
        [HttpGet]
        [Route("robots.txt")]
        public IActionResult Robots()
        {
            string content = string.Format(RobotsTemplate, _settings.SiteBaseUrl.TrimEnd('/'));
            return Content(content, RobotsContentType, Encoding.UTF8);
        }

        /// <summary>Serves sitemap.xml.</summary>
        [HttpGet]
        [Route("sitemap.xml")]
        public IActionResult Sitemap()
        {
            string baseUrl = _settings.SiteBaseUrl.TrimEnd('/');
            XNamespace ns = SitemapNamespace;
            XDocument document = new(
                new XElement(
                    ns + "urlset",
                    SitemapPaths.Select(path => new XElement(ns + "url", new XElement(ns + "loc", baseUrl + path)))));

            return Content(document.ToString(), SitemapContentType, Encoding.UTF8);
        }
    }
}
