using Microsoft.Extensions.DependencyInjection;

using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine
{
    /// <summary>Registers the Revathryn orchestration services.</summary>
    public static class EngineServiceCollectionExtensions
    {
        /// <summary>Adds the Revathryn engine services to the service collection.</summary>
        public static IServiceCollection AddEngineServices(this IServiceCollection services)
        {
            services.AddSingleton<IFileHasher, FileHasher>();
            services.AddSingleton<IRepositoryScanner, RepositoryScanner>();
            services.AddSingleton<IProjectDiscoveryService, ProjectDiscoveryService>();
            services.AddSingleton<IFileChangeMonitor, FileChangeMonitor>();
            services.AddSingleton<RepositoryScanLockRegistry>();

            services.AddSingleton<IReviewQueue, ReviewQueue>();
            services.AddSingleton<IReviewScheduler, ReviewScheduler>();
            services.AddSingleton<IReviewProcessor, ReviewProcessor>();
            services.AddSingleton<IRepositoryService, RepositoryService>();
            services.AddSingleton<IRepositoryFileService, RepositoryFileService>();
            services.AddSingleton<IRepositoryProcessor, RepositoryProcessor>();

            return services;
        }
    }
}
