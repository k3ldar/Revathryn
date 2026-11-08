using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;

using Revathryn.Website.Models;

namespace Revathryn.Website.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class WebsiteSettingsBindingTests
    {
        [TestMethod]
        public void Bind_CompleteSection_PopulatesAllProperties()
        {
            Dictionary<string, string> values = new()
            {
                ["Website:ProjectName"] = "Revathryn",
                ["Website:WebsiteTitle"] = "Revathryn – Title",
                ["Website:WebsiteDescription"] = "Description",
                ["Website:SiteBaseUrl"] = "https://revathryn.net",
                ["Website:GitHubRepositoryUrl"] = "https://github.com/k3ldar/Revathryn",
                ["Website:GitHubIssuesUrl"] = "https://github.com/k3ldar/Revathryn/issues",
                ["Website:GitHubDiscussionsUrl"] = "https://github.com/k3ldar/Revathryn/discussions",
                ["Website:DocumentationUrl"] = "https://github.com/k3ldar/Revathryn/docs",
                ["Website:LicenceUrl"] = "https://github.com/k3ldar/Revathryn/blob/main/LICENCE",
                ["Website:ContactEmail"] = "contact@revathryn.net"
            };

            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

            WebsiteSettings settings = configuration.GetSection(WebsiteSettings.SectionName).Get<WebsiteSettings>();

            Assert.IsNotNull(settings);
            Assert.AreEqual("Revathryn", settings.ProjectName);
            Assert.AreEqual("Revathryn – Title", settings.WebsiteTitle);
            Assert.AreEqual("Description", settings.WebsiteDescription);
            Assert.AreEqual("https://revathryn.net", settings.SiteBaseUrl);
            Assert.AreEqual("https://github.com/k3ldar/Revathryn", settings.GitHubRepositoryUrl);
            Assert.AreEqual("https://github.com/k3ldar/Revathryn/issues", settings.GitHubIssuesUrl);
            Assert.AreEqual("https://github.com/k3ldar/Revathryn/discussions", settings.GitHubDiscussionsUrl);
            Assert.AreEqual("https://github.com/k3ldar/Revathryn/docs", settings.DocumentationUrl);
            Assert.AreEqual("https://github.com/k3ldar/Revathryn/blob/main/LICENCE", settings.LicenceUrl);
            Assert.AreEqual("contact@revathryn.net", settings.ContactEmail);
        }

        [TestMethod]
        public void Bind_MissingSection_ReturnsNull()
        {
            IConfiguration configuration = new ConfigurationBuilder().Build();

            WebsiteSettings settings = configuration.GetSection(WebsiteSettings.SectionName).Get<WebsiteSettings>();

            Assert.IsNull(settings);
        }

        [TestMethod]
        public void Bind_PartialSection_LeavesMissingPropertiesNull()
        {
            Dictionary<string, string> values = new()
            {
                ["Website:ProjectName"] = "Revathryn"
            };

            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

            WebsiteSettings settings = configuration.GetSection(WebsiteSettings.SectionName).Get<WebsiteSettings>();

            Assert.IsNotNull(settings);
            Assert.AreEqual("Revathryn", settings.ProjectName);
            Assert.IsNull(settings.WebsiteTitle);
            Assert.IsNull(settings.GitHubRepositoryUrl);
        }
    }
}
