using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Revathryn.Website.Controllers;
using Revathryn.Website.Models;

namespace Revathryn.Website.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class HomeControllerTests
    {
        [TestMethod]
        public void Index_ReturnsViewResult()
        {
            HomeController controller = new(CreateSettings());

            IActionResult actionResult = controller.Index();

            Assert.IsInstanceOfType<ViewResult>(actionResult);
        }

        [TestMethod]
        public void Index_SetsDescriptionAndCanonicalPath()
        {
            HomeController controller = new(CreateSettings());

            controller.Index();

            Assert.AreEqual("Test description", controller.ViewData["Description"]);
            Assert.AreEqual("/", controller.ViewData["CanonicalPath"]);
        }

        [TestMethod]
        public void Error_WithoutStatusCode_Returns500()
        {
            HomeController controller = CreateControllerWithHttpContext();

            ErrorViewModel model = InvokeError(controller, null);

            Assert.AreEqual(StatusCodes.Status500InternalServerError, model.StatusCode);
        }

        [TestMethod]
        public void Error_With404_Returns404()
        {
            HomeController controller = CreateControllerWithHttpContext();

            ErrorViewModel model = InvokeError(controller, StatusCodes.Status404NotFound);

            Assert.AreEqual(StatusCodes.Status404NotFound, model.StatusCode);
        }

        [TestMethod]
        public void Error_WithCustomStatus_ReturnsCustomStatus()
        {
            HomeController controller = CreateControllerWithHttpContext();

            ErrorViewModel model = InvokeError(controller, 499);

            Assert.AreEqual(499, model.StatusCode);
        }

        [TestMethod]
        public void Error_SetsRequestId()
        {
            HomeController controller = CreateControllerWithHttpContext();

            ErrorViewModel model = InvokeError(controller, 500);

            Assert.IsFalse(string.IsNullOrWhiteSpace(model.RequestId));
            Assert.IsTrue(model.ShowRequestId);
        }

        private static WebsiteSettings CreateSettings()
        {
            return new WebsiteSettings { WebsiteDescription = "Test description" };
        }

        private static HomeController CreateControllerWithHttpContext()
        {
            HomeController controller = new(CreateSettings());
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            return controller;
        }

        private static ErrorViewModel InvokeError(HomeController controller, int? statusCode)
        {
            IActionResult actionResult = controller.Error(statusCode);
            ViewResult result = (ViewResult)actionResult;
            return (ErrorViewModel)result.Model;
        }
    }
}
