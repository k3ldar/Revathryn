using Microsoft.Extensions.DependencyInjection;

using Revathryn.Contracts;
using Revathryn.Contracts.Analysis;
using Revathryn.Engine;
using Revathryn.Review.CSharp;

namespace Revathryn.Review
{
    /// <summary>Registers the review engine services.</summary>
    public static class ReviewServiceCollectionExtensions
    {
        /// <summary>Adds the Revathryn review engines to the service collection.</summary>
        /// <param name="services">The service collection.</param>
        /// <param name="settings">The Revathryn settings.</param>
        public static IServiceCollection AddReviewServices(this IServiceCollection services, RevathrynSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));
            ArgumentNullException.ThrowIfNull(settings, nameof(settings));

            ReviewOptions options = new(settings);

            services.AddSingleton(options);
            services.AddSingleton<IAnalyser>(_ => new CSharpAnalyser(options.AnalysisOutputDirectory));
            services.AddSingleton<IManifestManager>(_ => new ManifestManager(options.AnalysisOutputDirectory));
            services.AddSingleton<ICSharpAnalysisEngine, CSharpAnalysisEngine>();
            services.AddSingleton<IAnalysisEngine, CSharpAnalysisEngine>();
            services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(Consts.OpenAIRequestTimeoutSeconds) });
            services.AddSingleton<IAIReviewEngine, OpenAIReviewEngine>();
            services.AddSingleton<IAICompletionClient, OpenAICompletionClient>();
            services.AddSingleton<IIssueValidationEngine, AIIssueValidationEngine>();
            services.AddSingleton<IPromptAnalysisEngine, AIPromptAnalysisEngine>();
            services.AddSingleton<IPromptProposalEngine, AIPromptProposalEngine>();
            services.AddSingleton<IReviewContextProvider, SymbolContextProvider>();

            return services;
        }
    }
}
