using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Orchestration;
using Revathryn.Contracts.Services;
using Revathryn.Data;
using Revathryn.Engine;
using Revathryn.Review;

namespace Revathryn.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class StartupTests
    {
        [TestMethod]
        public void ServiceProvider_ResolvesCoreServices()
        {
            Contracts.RevathrynSettings settings = new(Path.GetTempPath());
            ServiceCollection services = new();

            services.AddLogging();
            services.AddControllersWithViews();
            services.AddDataServices(settings);
            services.AddEngineServices();
            services.AddReviewServices(settings);

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.IsNotNull(provider.GetRequiredService<IDatabaseInitializer>());
            Assert.IsNotNull(provider.GetRequiredService<IDatabaseContext>());
            Assert.IsNotNull(provider.GetRequiredService<IRepositoryService>());
            Assert.IsNotNull(provider.GetRequiredService<IRepositoryFileService>());
            Assert.IsNotNull(provider.GetRequiredService<IReviewQueue>());
            Assert.IsNotNull(provider.GetRequiredService<IReviewScheduler>());
            Assert.IsNotNull(provider.GetRequiredService<IReviewProcessor>());
            Assert.IsNotNull(provider.GetRequiredService<ICSharpAnalysisEngine>());
            Assert.IsNotNull(provider.GetRequiredService<IAIReviewEngine>());
            Assert.IsNotNull(provider.GetRequiredService<IReviewContextProvider>());
            Assert.IsNotNull(provider.GetRequiredService<IBackupService>());
        }
    }
}
