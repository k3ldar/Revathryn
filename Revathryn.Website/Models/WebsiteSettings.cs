namespace Revathryn.Website.Models
{
    /// <summary>Central website configuration bound from the Website configuration section.</summary>
    public sealed class WebsiteSettings
    {
        /// <summary>The name of the configuration section used to bind these settings.</summary>
        public const string SectionName = "Website";

        /// <summary>Gets or sets the project name.</summary>
        public string ProjectName { get; set; }

        /// <summary>Gets or sets the website title used in the browser title and metadata.</summary>
        public string WebsiteTitle { get; set; }

        /// <summary>Gets or sets the website description used in metadata.</summary>
        public string WebsiteDescription { get; set; }

        /// <summary>Gets or sets the canonical base URL of the website.</summary>
        public string SiteBaseUrl { get; set; }

        /// <summary>Gets or sets the GitHub repository URL.</summary>
        public string GitHubRepositoryUrl { get; set; }

        /// <summary>Gets or sets the GitHub issues URL.</summary>
        public string GitHubIssuesUrl { get; set; }

        /// <summary>Gets or sets the GitHub discussions URL.</summary>
        public string GitHubDiscussionsUrl { get; set; }

        /// <summary>Gets or sets the documentation URL.</summary>
        public string DocumentationUrl { get; set; }

        /// <summary>Gets or sets the licence URL.</summary>
        public string LicenceUrl { get; set; }

        /// <summary>Gets or sets the contact email address.</summary>
        public string ContactEmail { get; set; }
    }
}
