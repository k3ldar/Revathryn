using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Website.Controllers;
using Revathryn.Website.Models;

namespace Revathryn.Website.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class SeoControllerTests
    {
        [TestMethod]
        public void Robots_ReturnsTextPlain()
        {
            SeoController controller = new(CreateSettings("https://revathryn.net"));

            ContentResult result = (ContentResult)controller.Robots();

            StringAssert.StartsWith(result.ContentType, "text/plain");
        }

        [TestMethod]
        public void Robots_IncludesSitemapUrl()
        {
            SeoController controller = new(CreateSettings("https://revathryn.net"));

            ContentResult result = (ContentResult)controller.Robots();

            StringAssert.Contains(result.Content, "Sitemap: https://revathryn.net/sitemap.xml");
        }

        [TestMethod]
        public void Robots_TrimsTrailingSlash()
        {
            SeoController controller = new(CreateSettings("https://revathryn.net/"));

            ContentResult result = (ContentResult)controller.Robots();

            StringAssert.Contains(result.Content, "Sitemap: https://revathryn.net/sitemap.xml");
            Assert.IsFalse(result.Content.Contains("//sitemap.xml", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Sitemap_ReturnsXml()
        {
            SeoController controller = new(CreateSettings("https://revathryn.net"));

            ContentResult result = (ContentResult)controller.Sitemap();

            StringAssert.StartsWith(result.ContentType, "application/xml");
        }

        [TestMethod]
        public void Sitemap_IncludesAllPaths()
        {
            SeoController controller = new(CreateSettings("https://revathryn.net"));

            ContentResult result = (ContentResult)controller.Sitemap();

            StringAssert.Contains(result.Content, "<loc>https://revathryn.net/</loc>");
            StringAssert.Contains(result.Content, "<loc>https://revathryn.net/roadmap</loc>");
            StringAssert.Contains(result.Content, "<loc>https://revathryn.net/about</loc>");
            StringAssert.Contains(result.Content, "<loc>https://revathryn.net/documentation</loc>");
        }

        private static WebsiteSettings CreateSettings(string siteBaseUrl)
        {
            return new WebsiteSettings { SiteBaseUrl = siteBaseUrl };
        }
    }
}
