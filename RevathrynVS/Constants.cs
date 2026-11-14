namespace RevathrynVS2026
{
    /// <summary>Non-localised values reused across the extension.</summary>
    internal static class Constants
    {
        #region Fields
        public const string DefaultEndpointUrl = "http://localhost:5000";

        public const string FileDetailsApiRoute = "api/files/details";

        public const string IssueSeverityRouteFormat = "issues/{0}/severity";

        public const string IssueStatusRouteFormat = "issues/{0}/status";

        public const string IssueResubmitRouteFormat = "issues/{0}/resubmit";

        public const string IssueDetailsWebRouteFormat = "Issues/Details/{0}";

        public const char PathSeparatorCharacter = '/';

        public const string PathQueryParameter = "path";

        public const string RepositoryQueryParameter = "repo";

        public const string FileQueryParameter = "file";

        public const string GitMarkerName = ".git";

        public const int RequestTimeoutSeconds = 10;

        public const int ShowPaneCommandId = 0x0100;

        public const int ConfigureCommandId = 0x0101;

        public const string ShowPaneFaultEventName = "RevathrynVS2026/ActiveFile/ShowPane";

        public const char QueryStartCharacter = '?';

        public const string ToolWindowTitle = "Revathryn Insights";

        public const string ConfigureWindowTitle = "Revathryn Configuration";

        public const string ConfigureFaultEventName = "RevathrynVS2026/Configure/Show";

        public const string OptionsCategory = "Revathryn";

        public const string OptionsPageName = "General";
        #endregion Fields
    }
}
