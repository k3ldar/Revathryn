using Microsoft.Extensions.DependencyInjection;

using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>Registers the SQLite data services.</summary>
    public static class DataServiceCollectionExtensions
    {
        /// <summary>Adds the Revathryn persistence layer to the service collection.</summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional callback for customising database options.</param>
        public static IServiceCollection AddDataServices(this IServiceCollection services, Action<DatabaseOptions> configure = null)
        {
            DatabaseOptions options = new();
            configure?.Invoke(options);

            services.AddSingleton(options);
            services.AddSingleton<SqliteConnectionFactory>();
            services.AddSingleton<IDatabaseContext, SqliteDatabaseContext>();
            services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();

            services.AddScoped<IRepositoryRepository, RepositoryRepository>();
            services.AddScoped<IRepoFileRepository, RepoFileRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IRepositoryScanRepository, RepositoryScanRepository>();
            services.AddScoped<IReviewTypeRepository, ReviewTypeRepository>();
            services.AddScoped<IReviewLinkRepository, ReviewLinkRepository>();
            services.AddScoped<IRepoFileAnalysisRepository, RepoFileAnalysisRepository>();
            services.AddScoped<IFindingRepository, FindingRepository>();
            services.AddScoped<IQueueRepository, QueueRepository>();
            services.AddScoped<IAIEngineRepository, AIEngineRepository>();
            services.AddScoped<IAISettingsRepository, AISettingsRepository>();
            services.AddScoped<IBackupService, BackupService>();

            return services;
        }
    }
}
