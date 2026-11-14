using Newtonsoft.Json.Linq;

namespace RevathrynVS2026.Services
{
    /// <summary>Retrieves JSON data for a file from the local endpoint.</summary>
    internal interface IActiveFileEndpointClient
    {
        #region Public Methods
        /// <summary>Requests the JSON data for <paramref name="filePath"/>.</summary>
        /// <param name="filePath">The full path of the file.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The parsed response, or <see langword="null"/> when the response body is empty.</returns>
        Task<JToken> GetAsync(string filePath, CancellationToken cancellationToken);

        /// <summary>Updates the severity of a finding.</summary>
        /// <param name="findingId">Finding identifier.</param>
        /// <param name="severity">New severity value.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The parsed endpoint response.</returns>
        Task<JToken> UpdateSeverityAsync(long findingId, int severity, CancellationToken cancellationToken);

        /// <summary>Updates the status of a finding.</summary>
        /// <param name="findingId">Finding identifier.</param>
        /// <param name="status">New status value.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The parsed endpoint response.</returns>
        Task<JToken> UpdateStatusAsync(long findingId, int status, CancellationToken cancellationToken);

        /// <summary>Re-queues a finding's file for review.</summary>
        /// <param name="findingId">Finding identifier.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The parsed endpoint response.</returns>
        Task<JToken> ResubmitAsync(long findingId, CancellationToken cancellationToken);

        /// <summary>Builds the browser URL for a finding on the website.</summary>
        /// <param name="findingId">Finding identifier.</param>
        /// <returns>The absolute issue details URL.</returns>
        Uri GetIssueUrl(long findingId);
        #endregion Public Methods
    }
}
