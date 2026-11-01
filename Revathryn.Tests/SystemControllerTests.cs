using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Reliability;
using Revathryn.Controllers;
using Revathryn.Models;
using Revathryn.Models.SystemHealth;

namespace Revathryn.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class SystemControllerTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task Index_ReturnsViewWithHealthReportAndUptime()
        {
            HealthReport report = new()
            {
                Status = HealthStatus.Degraded
            };

            UptimeModel uptime = new();
            FakeHealthService healthService = new() { Report = report };

            ControllerContext context = ControllerTestSupport.CreateControllerContext(out _);
            SystemController controller = new(healthService, uptime)
            {
                ControllerContext = context,
                Url = new FakeUrlHelper(context)
            };

            IActionResult result = await controller.Index(TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            SystemHealthViewModel model = viewResult.Model as SystemHealthViewModel;
            Assert.IsNotNull(model);
            Assert.AreSame(report, model.HealthReport);
            Assert.AreEqual(uptime.StartTime, model.StartTimeUtc);
            Assert.IsGreaterThanOrEqualTo(TimeSpan.Zero, model.Uptime);
            Assert.IsLessThan(TimeSpan.FromSeconds(5), model.Uptime);

            List<BreadcrumbItem> breadcrumbs = viewResult.ViewData[Consts.BreadcrumbsViewDataKey] as List<BreadcrumbItem>;
            Assert.IsNotNull(breadcrumbs);
            Assert.HasCount(2, breadcrumbs);
            Assert.IsFalse(breadcrumbs[0].IsActive);
            Assert.IsTrue(breadcrumbs[1].IsActive);
        }

        [TestMethod]
        public async Task Index_ReportsHealthyStatusWhenServiceReturnsHealthy()
        {
            HealthReport report = new()
            {
                Status = HealthStatus.Healthy
            };

            UptimeModel uptime = new();
            FakeHealthService healthService = new() { Report = report };

            ControllerContext context = ControllerTestSupport.CreateControllerContext(out _);
            SystemController controller = new(healthService, uptime)
            {
                ControllerContext = context,
                Url = new FakeUrlHelper(context)
            };

            IActionResult result = await controller.Index(TestContext.CancellationToken);

            ViewResult viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult);

            SystemHealthViewModel model = viewResult.Model as SystemHealthViewModel;
            Assert.IsNotNull(model);
            Assert.AreEqual(HealthStatus.Healthy, model.HealthReport.Status);
        }

        private sealed class FakeHealthService : IHealthService
        {
            public HealthReport Report { get; set; } = new();

            public Task<HealthReport> GetReadinessAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Report);
            }

            public HealthReport GetLiveness()
            {
                return Report;
            }
        }
    }
}
