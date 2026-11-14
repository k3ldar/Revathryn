using RevathrynVS2026.ToolWindows;

namespace RevathrynVS2026.Configuration
{
    /// <summary>Edits the server URL shown in the configure window.</summary>
    internal sealed class ConfigureViewModel : ViewModelBase
    {
        #region Fields
        private string endpointUrl;

        private string repository;

        private string errorText;
        #endregion Fields

        #region Constructors
        /// <summary>Initializes a new instance of the <see cref="ConfigureViewModel"/> class.</summary>
        /// <param name="endpointUrl">The currently configured server URL.</param>
        /// <param name="repository">The currently configured repository override, or empty to detect automatically.</param>
        public ConfigureViewModel(string endpointUrl, string repository)
        {
            this.endpointUrl = endpointUrl;
            this.repository = repository;
        }

        #endregion Constructors

        #region Properties
        /// <summary>Gets or sets the server URL being edited.</summary>
        public string EndpointUrl
        {
            get => endpointUrl;
            set
            {
                if (SetProperty(ref endpointUrl, value))
                {
                    ErrorText = null;
                }
            }
        }

        /// <summary>Gets or sets the repository override being edited; empty means detect automatically.</summary>
        public string Repository
        {
            get => repository;
            set => SetProperty(ref repository, value);
        }

        /// <summary>Gets the validation message, or null when the value is valid.</summary>
        public string ErrorText
        {
            get => errorText;
            private set => SetProperty(ref errorText, value);
        }

        #endregion Properties

        #region Public Methods
        /// <summary>Validates the server URL and, when valid, normalizes it.</summary>
        /// <returns>True when the server URL is an absolute http or https URL.</returns>
        public bool TryValidate()
        {
            string value = EndpointUrl?.Trim();

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ErrorText = ConfigureMessages.InvalidEndpoint;
                return false;
            }

            EndpointUrl = value;
            Repository = Repository?.Trim();
            return true;
        }

        #endregion Public Methods
    }
}
