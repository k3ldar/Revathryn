using Revathryn.Contracts.Infrastructure;
using Revathryn.Data;
using Revathryn.Engine;
using Revathryn.Review;

#pragma warning disable CC0009

namespace Revathryn
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddDataServices(options => options.ConnectionString = "Data Source=revathryn.db");
            builder.Services.AddEngineServices();
            builder.Services.AddReviewServices();

            WebApplication app = builder.Build();

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
    }
}

#pragma warning restore CC0009