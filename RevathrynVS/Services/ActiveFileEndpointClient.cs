using System.IO;
using System.Net.Http;
using System.Text;

using Newtonsoft.Json.Linq;

namespace RevathrynVS2026.Services
{
    /// <summary>Calls the local endpoint over HTTP with the repository (detected automatically unless overridden in the settings) and file name, or the full file path when there is no repository.</summary>
    internal sealed class ActiveFileEndpointClient : IActiveFileEndpointClient
    {
        #region Fields
        private const string MediaTypeJson = "application/json";

        private readonly HttpClient httpClient;

        private readonly IEndpointSettings settings;
        #endregion Fields

        #region Constructors
        public ActiveFileEndpointClient(HttpClient httpClient, IEndpointSettings settings)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            this.httpClient = httpClient;
            this.settings = settings;
        }

        #endregion Constructors

        #region Public Methods
        public async Task<JToken> GetAsync(string filePath, CancellationToken cancellationToken)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            Uri requestUri = BuildRequestUri(settings.EndpointUrl, settings.Repository, filePath);
            using (HttpResponseMessage response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(true))
            {
                return await ReadResponseAsync(response).ConfigureAwait(true);
            }
        }

        public async Task<JToken> UpdateSeverityAsync(long findingId, int severity, CancellationToken cancellationToken)
        {
            Uri requestUri = BuildIssueActionUri(settings.EndpointUrl, string.Format(Constants.IssueSeverityRouteFormat, findingId));
            JObject body = new()
            {
                ["severity"] = severity
            };

            using (StringContent content = new(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, MediaTypeJson))
            using (HttpResponseMessage response = await httpClient.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(true))
            {
                return await ReadResponseAsync(response).ConfigureAwait(true);
            }
        }

        public async Task<JToken> UpdateStatusAsync(long findingId, int status, CancellationToken cancellationToken)
        {
            Uri requestUri = BuildIssueActionUri(settings.EndpointUrl, string.Format(Constants.IssueStatusRouteFormat, findingId));
            JObject body = new()
            {
                ["status"] = status
            };

            using (StringContent content = new(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, MediaTypeJson))
            using (HttpResponseMessage response = await httpClient.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(true))
            {
                return await ReadResponseAsync(response).ConfigureAwait(true);
            }
        }

        public async Task<JToken> ResubmitAsync(long findingId, CancellationToken cancellationToken)
        {
            Uri requestUri = BuildIssueActionUri(settings.EndpointUrl, string.Format(Constants.IssueResubmitRouteFormat, findingId));
            using (HttpResponseMessage response = await httpClient.PostAsync(requestUri, content: null, cancellationToken).ConfigureAwait(true))
            {
                return await ReadResponseAsync(response).ConfigureAwait(true);
            }
        }

        public Uri GetIssueUrl(long findingId)
        {
            Uri endpoint = ResolveEndpoint(settings.EndpointUrl);
            UriBuilder builder = new(endpoint);
            string basePath = endpoint.AbsolutePath.TrimEnd(Constants.PathSeparatorCharacter);
            builder.Path = basePath + Constants.PathSeparatorCharacter + string.Format(Constants.IssueDetailsWebRouteFormat, findingId);
            builder.Query = string.Empty;
            return builder.Uri;
        }

        #endregion Public Methods

        #region Private Methods
        private static async Task<JToken> ReadResponseAsync(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
            return string.IsNullOrWhiteSpace(body) ? null : JToken.Parse(body);
        }

        private static Uri BuildRequestUri(string endpointUrl, string configuredRepository, string filePath)
        {
            Uri endpoint = ResolveEndpoint(endpointUrl);
            UriBuilder builder = new(endpoint);
            string basePath = endpoint.AbsolutePath.TrimEnd(Constants.PathSeparatorCharacter);
            builder.Path = basePath + Constants.PathSeparatorCharacter + Constants.FileDetailsApiRoute;
            string repository = string.IsNullOrWhiteSpace(configuredRepository)
                ? RepositoryLocator.FindRepositoryName(filePath)
                : configuredRepository.Trim();
            string parameter = string.IsNullOrEmpty(repository)
                ? $"{Constants.PathQueryParameter}={Uri.EscapeDataString(filePath)}"
                : $"{Constants.RepositoryQueryParameter}={Uri.EscapeDataString(repository)}&{Constants.FileQueryParameter}={Uri.EscapeDataString(Path.GetFileName(filePath))}";
            builder.Query = string.IsNullOrEmpty(builder.Query)
                ? parameter
                : $"{builder.Query.TrimStart(Constants.QueryStartCharacter)}&{parameter}";
            return builder.Uri;
        }

        private static Uri BuildIssueActionUri(string endpointUrl, string route)
        {
            Uri endpoint = ResolveEndpoint(endpointUrl);
            UriBuilder builder = new(endpoint);
            string basePath = endpoint.AbsolutePath.TrimEnd(Constants.PathSeparatorCharacter);
            builder.Path = basePath + Constants.PathSeparatorCharacter + Constants.FileDetailsApiRoute + Constants.PathSeparatorCharacter + route;
            builder.Query = string.Empty;
            return builder.Uri;
        }

        private static Uri ResolveEndpoint(string endpointUrl)
        {
            if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out Uri endpoint))
            {
                throw new InvalidOperationException(ActiveFileMessages.EndpointNotConfigured);
            }

            return endpoint;
        }

        #endregion Private Methods
    }
}
