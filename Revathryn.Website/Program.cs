using Revathryn.Website.Models;
using Revathryn.Website.Services;

namespace Revathryn.Website
{
    /// <summary>Application entry point for the Revathryn public website.</summary>
    public class Program
    {
        private const string DefaultRouteName = "default";
        private const string DefaultRoutePattern = "{controller=Home}/{action=Index}/{id?}";
        private const string ErrorRoute = "/Home/Error";
        private const string StatusCodeQuery = "?statusCode={0}";

        /// <summary>The application entry point.</summary>
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            WebsiteSettings websiteSettings = builder.Configuration.GetSection(WebsiteSettings.SectionName).Get<WebsiteSettings>() ?? new WebsiteSettings();

            builder.Services.AddSingleton(websiteSettings);
            builder.Services.AddSingleton<IRoadmapService, RoadmapService>();
            builder.Services.AddControllersWithViews();

            WebApplication app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler(ErrorRoute);
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute(ErrorRoute, StatusCodeQuery);

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: DefaultRouteName,
                pattern: DefaultRoutePattern);

            app.Run();
        }
    }
}
