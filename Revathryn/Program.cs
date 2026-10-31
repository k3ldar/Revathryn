using System.Diagnostics.CodeAnalysis;

using AspNetCore.PluginManager;

using PluginManager;

using Revathryn.Contracts.Infrastructure;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Reliability;
using Revathryn.Data;
using Revathryn.Engine;
using Revathryn.Internal;
using Revathryn.Review;

using Shared.Classes;

#pragma warning disable CC0009

namespace Revathryn
{
    [ExcludeFromCodeCoverage(Justification = "Not unit testable.")]
    public class Program
    {
        public static void Main(string[] args)
        {
            ThreadManager.Initialise(new SharedLib.Win.WindowsCpuUsage());
            ThreadManager.MaximumPoolSize = Contracts.Consts.MaximumThreadPoolSize;
            ThreadManager.MaximumRunningThreads = Contracts.Consts.MaximumConcurrentRunningThreads;
            ThreadManager.ThreadCpuChangeNotification = 0;


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


                // Add services to the container.
                builder.Services.AddSingleton(new UptimeModel());
                builder.Services.AddControllersWithViews();
                builder.Services.AddDataServices();
                builder.Services.AddEngineServices();
                builder.Services.AddSingleton<IOperationalLogSink, OperationalLoggingSink>();
                builder.Services.AddReviewServices();
                builder.Services.AddHostedService<ReviewWorkerService>();

                //builder.Host.UseWindowsService();
                //builder.Host.UseSystemd();

                WebApplication app = builder.Build();
                PluginManagerService.Configure(app);


                // Initialise the persistence store before serving requests.
                IDatabaseInitializer databaseInitializer = app.Services.GetRequiredService<IDatabaseInitializer>();
                databaseInitializer.Initialize();

                // Configure the HTTP request pipeline.
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Home/Error");
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }

                app.UseHttpsRedirection();
                app.UseRouting();

                app.UseAuthorization();

                app.MapStaticAssets();
                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}")
                    .WithStaticAssets();

                app.Run();
            }
            finally
            {
                ThreadManager.CancelAll();
                PluginManagerService.Finalise();
            }
        }
    }
}

#pragma warning restore CC0009