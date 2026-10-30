using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;
using Revathryn.Models;

namespace Revathryn.Controllers
{
    /// <summary>Administration dashboard controller.</summary>
    public class DashboardController : Controller
    {
        private readonly IRepositoryService _repositoryService;

        /// <summary>Initialises a new controller.</summary>
        public DashboardController(IRepositoryService repositoryService)
        {
            _repositoryService = repositoryService;
        }

        /// <summary>Renders the administration dashboard.</summary>
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            IReadOnlyList<Repository> repositories = await _repositoryService.GetAllAsync(cancellationToken);

            DashboardViewModel model = new()
            {
                Repositories = repositories
            };

            return View(model);
        }
    }
}
