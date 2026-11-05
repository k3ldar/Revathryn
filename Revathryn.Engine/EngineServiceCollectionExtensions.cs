using Microsoft.Extensions.DependencyInjection;

using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Engine.Reliability;

namespace Revathryn.Engine
{
    /// <summary>Registers the Revathryn orchestration services.</summary>
    public static class EngineServiceCollectionExtensions
    {
        /// <summary>Adds the Revathryn engine services to the service collection.</summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional callback for customising resilience options.</param>
        public static IServiceCollection AddEngineServices(this IServiceCollection services, Action<ResilienceOptions> configure = null)
        {
            ResilienceOptions resilienceOptions = new();
            configure?.Invoke(resilienceOptions);

            services.AddSingleton(resilienceOptions);
            services.AddSingleton<ICorrelationContext, CorrelationContext>();
            services.AddSingleton<IFailureClassifier, FailureClassifier>();
            services.AddSingleton<IOperationalDiagnostics, OperationalDiagnostics>();
            services.AddSingleton<IRetryPolicyProvider, RetryPolicyProvider>();
            services.AddSingleton<IRetryExecutor, RetryExecutor>();

            services.AddSingleton<IFileHasher, FileHasher>();
            services.AddSingleton<IRepositoryScanner, RepositoryScanner>();
            services.AddSingleton<IProjectDiscoveryService, ProjectDiscoveryService>();
            services.AddSingleton<IFileChangeMonitor, FileChangeMonitor>();
            services.AddSingleton<RepositoryScanLockRegistry>();

            services.AddSingleton<IReviewQueue, ReviewQueue>();
            services.AddSingleton<IReviewScheduler, ReviewScheduler>();
            services.AddSingleton<IReviewProcessor, ReviewProcessor>();
            services.AddSingleton<IRepositoryService, RepositoryService>();
            services.AddSingleton<IReviewTypeSeeder, ReviewTypeSeeder>();
            services.AddSingleton<IRepositoryFileService, RepositoryFileService>();
            services.AddSingleton<IRepositoryProcessor, RepositoryProcessor>();
            services.AddSingleton<IHealthService, HealthService>();
            services.AddSingleton<IIssueQueryService, IssueQueryService>();
            services.AddSingleton<IDashboardQueryService, DashboardQueryService>();

            services.AddSingleton<IReviewEngineConfigurationService, ReviewEngineConfigurationService>();
            services.AddSingleton<IIssueValidationContextBuilder, IssueValidationContextBuilder>();
            services.AddSingleton<IIssueValidationService, IssueValidationService>();
            services.AddSingleton<IIssueValidationProcessor, IssueValidationProcessor>();
            services.AddSingleton<IIssueSettingsProvider, IssueSettingsProvider>();
            services.AddSingleton<ISeverityRuleProvider, SeverityRuleProvider>();
            services.AddSingleton<ISeverityRuleEngine, SeverityRuleEngine>();
            services.AddSingleton<ISeverityRuleSeeder, SeverityRuleSeeder>();
            services.AddSingleton<IIssueSimilarityService, IssueSimilarityService>();
            services.AddSingleton<IDuplicateDetectionProcessor, DuplicateDetectionProcessor>();
            services.AddSingleton<IPromptResolutionService, PromptResolutionService>();
            services.AddSingleton<IPromptOptimisationContextBuilder, PromptOptimisationContextBuilder>();
            services.AddSingleton<IPromptOptimisationProcessor, PromptOptimisationProcessor>();
            services.AddSingleton<IPromptOptimisationService, PromptOptimisationService>();
            services.AddSingleton<IPromptTestRunProcessor, PromptTestRunProcessor>();
            services.AddSingleton<IPromptTestService, PromptTestService>();
            services.AddSingleton<IPromptManagementService, PromptManagementService>();
            services.AddSingleton<IPromptStatisticsService, PromptStatisticsService>();

            return services;
        }
    }
}
