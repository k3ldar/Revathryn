using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Website.Controllers;

namespace Revathryn.Website.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class DocumentationControllerTests
    {
        [TestMethod]
        public void Index_ReturnsViewResult()
        {
            DocumentationController controller = new();

            IActionResult actionResult = controller.Index();

            Assert.IsInstanceOfType<ViewResult>(actionResult);
        }

        [TestMethod]
        public void Index_SetsViewData()
        {
            DocumentationController controller = new();

            controller.Index();

            Assert.AreEqual("Documentation", controller.ViewData["Title"]);
            Assert.AreEqual("/documentation", controller.ViewData["CanonicalPath"]);
            Assert.IsFalse(string.IsNullOrWhiteSpace(controller.ViewData["Description"] as string));
        }
    }
}
