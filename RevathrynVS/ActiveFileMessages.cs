namespace RevathrynVS2026
{
    /// <summary>User-visible status messages shown by the pane.</summary>
    internal static class ActiveFileMessages
    {
        #region Fields
        public const string NoActiveFile = "No active file.";

        public const string Loading = "Loading...";

        public const string NoData = "The endpoint returned no data for this file.";

        public const string Loaded = "Up to date.";

        public const string EndpointNotConfigured = "The server URL is not configured. Set it under Extensions > Revathryn > Configure.";

        public const string RequestTimedOut = "The endpoint did not respond in time.";

        public const string RequestFailedFormat = "The endpoint request failed: {0}";

        public const string InvalidJsonFormat = "The endpoint returned invalid JSON: {0}";

        #endregion Fields
    }
}
