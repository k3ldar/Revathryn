namespace Revathryn.Contracts.Models
{
    /// <summary>Global AI configuration.</summary>
    public class AISettings
    {
        /// <summary>The configurable global master prompt prepended to every review prompt.</summary>
        public string MasterPrompt { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the settings were last modified.</summary>
        public DateTime LastModifiedDate { get; set; }
    }
}
