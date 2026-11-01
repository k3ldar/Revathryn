using System.Diagnostics.CodeAnalysis;

using AspNetCore.PluginManager;

using PluginManager;

using Revathryn.Contracts;
using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Reliability;
using Revathryn.Contracts.Services;
using Revathryn.Data;
using Revathryn.Engine;
using Revathryn.Internal;
using Revathryn.Review;

using Shared.Classes;

namespace Revathryn
{
    [ExcludeFromCodeCoverage(Justification = "Not unit testable.")]
    public class Program
    {
        public static async Task Main(string[] args)
        {
            ThreadManager.Initialise(new SharedLib.Win.WindowsCpuUsage());
            ThreadManager.MaximumPoolSize = Contracts.Consts.MaximumThreadPoolSize;
            ThreadManager.MaximumRunningThreads = Contracts.Consts.MaximumConcurrentRunningThreads;
            ThreadManager.ThreadCpuChangeNotification = Contracts.Consts.ThreadCpuChangeNotificationDisabledValue;


            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // plugin support
            PluginManagerService.UsePlugin(typeof(MemoryCache.Plugin.PluginInitialisation));

            PluginManager.Abstractions.ILogger logger = new RevathrynLogger();
            PluginManagerConfiguration configuration = new(logger)
            {
                ServiceConfigurator = new ServiceConfigurator()
            };

            PluginManagerService.Initialise(configuration);
            try
            {
                PluginManagerService.ConfigureServices(builder.Services);

                RevathrynSettings settings = new();
                builder.Services.AddSingleton(settings);

                // Add services to the container.
                builder.Services.AddSingleton(new UptimeModel());
                builder.Services.AddControllersWithViews();
                builder.Services.AddDataServices(settings);
                builder.Services.AddEngineServices();
                builder.Services.AddSingleton<IOperationalLogSink, OperationalLoggingSink>();
                builder.Services.AddReviewServices(settings);
                builder.Services.AddHostedService<ReviewWorkerService>();

                //builder.Host.UseWindowsService();
                //builder.Host.UseSystemd();

                WebApplication app = builder.Build();
                PluginManagerService.Configure(app);


                // Initialise the persistence store before serving requests.
                IDatabaseInitializer databaseInitializer = app.Services.GetRequiredService<IDatabaseInitializer>();
                await databaseInitializer.InitializeAsync();

                // Seed the default master prompt when none has been configured.
                IAISettingsRepository aiSettingsRepository = app.Services.GetRequiredService<IAISettingsRepository>();
                AISettings aiSettings = await aiSettingsRepository.GetAsync();

                if (string.IsNullOrWhiteSpace(aiSettings.MasterPrompt))
                {
                    aiSettings.MasterPrompt = DefaultReviewContext.DefaultMasterPrompt;
                    await aiSettingsRepository.UpdateAsync(aiSettings);
                }

                // Seed built-in review definitions for any repository that is missing them.
                IReviewTypeSeeder reviewTypeSeeder = app.Services.GetRequiredService<IReviewTypeSeeder>();
                IRepositoryService repositoryService = app.Services.GetRequiredService<IRepositoryService>();
                IReadOnlyList<Repository> repositories = await repositoryService.GetAllAsync();

                foreach (Repository repository in repositories)
                {
                    await reviewTypeSeeder.SeedAsync(repository.Id);
                }

                // Configure the HTTP request pipeline.
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler(Contracts.Consts.DashboardErrorRoute);
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }

                app.UseHttpsRedirection();
                app.UseRouting();

                app.UseAuthorization();

                app.MapStaticAssets();
                app.MapControllerRoute(
                    name: Contracts.Consts.DefaultRouteName,
                    pattern: Contracts.Consts.DefaultRoutePattern)
                    .WithStaticAssets();

                await app.RunAsync();
            }
            finally
            {
                ThreadManager.CancelAll();
                PluginManagerService.Finalise();
            }
        }
    }
}
