namespace RevathrynVS2026.Services
{
    /// <summary>Supplies the server configuration.</summary>
    internal interface IEndpointSettings
    {
        #region Properties
        /// <summary>Gets the absolute URL of the server hosting Revathryn.</summary>
        string EndpointUrl { get; }

        /// <summary>Gets the repository name that overrides automatic detection, or an empty value to detect it.</summary>
        string Repository { get; }
        #endregion Properties
    }
}
