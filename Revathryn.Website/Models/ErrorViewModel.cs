namespace Revathryn.Website.Models
{
    /// <summary>Model for the shared error page.</summary>
    public sealed class ErrorViewModel
    {
        /// <summary>Gets or sets the HTTP status code.</summary>
        public int StatusCode { get; set; }

        /// <summary>Gets or sets the request identifier.</summary>
        public string RequestId { get; set; }

        /// <summary>Gets a value indicating whether the request identifier should be shown.</summary>
        public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
    }
}
