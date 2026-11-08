using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Website.Controllers;

namespace Revathryn.Website.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class AboutControllerTests
    {
        [TestMethod]
        public void Index_ReturnsViewResult()
        {
            AboutController controller = new();

            IActionResult actionResult = controller.Index();

            Assert.IsInstanceOfType<ViewResult>(actionResult);
        }

        [TestMethod]
        public void Index_SetsViewData()
        {
            AboutController controller = new();

            controller.Index();

            Assert.AreEqual("About", controller.ViewData["Title"]);
            Assert.AreEqual("/about", controller.ViewData["CanonicalPath"]);
            Assert.IsFalse(string.IsNullOrWhiteSpace(controller.ViewData["Description"] as string));
        }
    }
}
