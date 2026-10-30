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
        /// <param name="configure">Optional callback for customising review options.</param>
        public static IServiceCollection AddReviewServices(this IServiceCollection services, Action<ReviewOptions> configure = null)
        {
            ReviewOptions options = new();
            configure?.Invoke(options);

            services.AddSingleton(options);
            services.AddSingleton<IAnalyser>(_ => new CSharpAnalyser(options.AnalysisOutputDirectory));
            services.AddSingleton<IManifestManager>(_ => new ManifestManager(options.ManifestPath));
            services.AddSingleton<ICSharpAnalysisEngine, CSharpAnalysisEngine>();
            services.AddSingleton<IAnalysisEngine, CSharpAnalysisEngine>();
            services.AddSingleton<IAIReviewEngine, StubAIReviewEngine>();
            services.AddSingleton<IReviewContextProvider, StubReviewContextProvider>();

            return services;
        }
    }
}
