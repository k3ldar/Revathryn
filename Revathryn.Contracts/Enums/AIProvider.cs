namespace Revathryn.Contracts.Enums
{
    /// <summary>Identifies the provider flavour of an AI engine.</summary>
    public enum AIProvider
    {
        /// <summary>A generic OpenAI-compatible endpoint.</summary>
        OpenAI = 0,

        /// <summary>Azure OpenAI Service.</summary>
        AzureOpenAI = 1,

        /// <summary>A locally hosted OpenAI-compatible endpoint, e.g. Ollama.</summary>
        Local = 2,

        /// <summary>Any other OpenAI-compatible provider.</summary>
        Custom = 3
    }
}
