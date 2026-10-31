namespace Revathryn.Contracts.Reliability
{
    /// <summary>Provides and scopes correlation identifiers for related work.</summary>
    public interface ICorrelationContext
    {
        /// <summary>The current correlation identifier, or an empty string.</summary>
        string Current { get; }

        /// <summary>Begins a scope with a new correlation identifier, or the current one.</summary>
        IDisposable Begin(string correlationId = null);
    }
}
