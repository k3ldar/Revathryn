using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Reliability;
using Revathryn.Models;
using Revathryn.Models.SystemHealth;
using Revathryn.Resources;

namespace Revathryn.Controllers
{
    /// <summary>System health and operational monitoring.</summary>
    public class SystemController : Controller
    {
        private readonly IHealthService _healthService;
        private readonly UptimeModel _uptimeModel;

        /// <summary>Initialises a new system controller.</summary>
        public SystemController(IHealthService healthService, UptimeModel uptimeModel)
        {
            _healthService = healthService;
            _uptimeModel = uptimeModel;
        }

        /// <summary>Renders the system health view.</summary>
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            HealthReport report = await _healthService.GetReadinessAsync(cancellationToken);

            SystemHealthViewModel model = new()
            {
                HealthReport = report,
                StartTimeUtc = _uptimeModel.StartTime,
                Uptime = _uptimeModel.Duration
            };

            ViewData[Consts.BreadcrumbsViewDataKey] = new List<BreadcrumbItem>
            {
                new() { Title = Strings.DashboardTitle, Url = Url.Action(nameof(DashboardController.Index), Consts.DashboardControllerName) },
                new() { Title = Strings.SystemHealthTitle, IsActive = true }
            };

            return View(model);
        }
    }
}
