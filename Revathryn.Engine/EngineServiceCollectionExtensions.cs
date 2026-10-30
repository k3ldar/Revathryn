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

            services.AddScoped<IReviewQueue, ReviewQueue>();
            services.AddScoped<IReviewScheduler, ReviewScheduler>();
            services.AddScoped<IReviewProcessor, ReviewProcessor>();
            services.AddScoped<IRepositoryService, RepositoryService>();
            services.AddScoped<IRepositoryFileService, RepositoryFileService>();
            services.AddScoped<IRepositoryProcessor, RepositoryProcessor>();

            return services;
        }
    }
}
