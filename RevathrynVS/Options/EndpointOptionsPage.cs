using System.ComponentModel;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;

using RevathrynVS2026.Services;

namespace RevathrynVS2026.Options
{
    /// <summary>Tools &gt; Options page holding the server configuration.</summary>
    [Guid(PackageGuids.OptionsPageString)]
    public sealed class EndpointOptionsPage : DialogPage, IEndpointSettings
    {
        #region Properties
        /// <summary>Gets or sets the absolute URL of the server hosting Revathryn.</summary>
        [Category("Endpoint")]
        [DisplayName("Server URL")]
        [Description("Absolute URL of the server hosting Revathryn. The file details route is appended automatically.")]
        public string EndpointUrl { get; set; } = Constants.DefaultEndpointUrl;

        /// <summary>Gets or sets the repository name that overrides automatic detection.</summary>
        [Category("Endpoint")]
        [DisplayName("Repository override")]
        [Description("Name sent as the repository. Leave empty to detect the Git repository of the active file automatically.")]
        public string Repository { get; set; }
        #endregion Properties
    }
}
