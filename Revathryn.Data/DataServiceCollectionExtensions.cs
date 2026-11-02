using Microsoft.Extensions.DependencyInjection;

using Revathryn.Contracts;
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
        public static IServiceCollection AddDataServices(this IServiceCollection services, RevathrynSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));
            ArgumentNullException.ThrowIfNull(settings, nameof(settings));

            DatabaseOptions options = new(settings);

            services.AddSingleton(settings);
            services.AddSingleton(options);
            services.AddSingleton<SqliteConnectionFactory>();
            services.AddSingleton<IDatabaseContext, SqliteDatabaseContext>();
            services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
            services.AddSingleton<ISecretsProvider, EnvironmentSecretsProvider>();

            services.AddTransient<IRepositoryRepository, RepositoryRepository>();
            services.AddTransient<IRepoFileRepository, RepoFileRepository>();
            services.AddTransient<IProjectRepository, ProjectRepository>();
            services.AddTransient<IRepositoryScanRepository, RepositoryScanRepository>();
            services.AddTransient<IReviewTypeRepository, ReviewTypeRepository>();
            services.AddTransient<IReviewLinkRepository, ReviewLinkRepository>();
            services.AddTransient<IRepoFileAnalysisRepository, RepoFileAnalysisRepository>();
            services.AddTransient<IFindingRepository, FindingRepository>();
            services.AddTransient<IReviewContextRepository, ReviewContextRepository>();
            services.AddTransient<IReviewOutcomeStore, ReviewOutcomeStore>();
            services.AddTransient<IQueueRepository, QueueRepository>();
            services.AddTransient<IAIEngineRepository, AIEngineRepository>();
            services.AddTransient<IReviewUsageRepository, ReviewUsageRepository>();
            services.AddTransient<IAISettingsRepository, AISettingsRepository>();
            services.AddTransient<IReviewEngineSettingsRepository, ReviewEngineSettingsRepository>();
            services.AddTransient<IIssueValidationRepository, IssueValidationRepository>();
            services.AddTransient<IBackupService, BackupService>();

            return services;
        }
    }
}
