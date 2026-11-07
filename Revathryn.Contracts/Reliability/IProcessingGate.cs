namespace Revathryn.Contracts.Reliability
{
    /// <summary>
    /// Coordinates halting of automated processing when a fatal configuration problem is detected.
    /// </summary>
    public interface IProcessingGate
    {
        /// <summary>Whether automated processing is currently paused.</summary>
        bool IsPaused { get; }

        /// <summary>Reason processing is paused, or empty when processing is active.</summary>
        string Reason { get; }

        /// <summary>Pauses automated processing with the supplied reason.</summary>
        void Pause(string reason);

        /// <summary>Resumes automated processing.</summary>
        void Resume();
    }
}
