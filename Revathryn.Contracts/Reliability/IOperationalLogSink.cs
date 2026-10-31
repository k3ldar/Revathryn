namespace Revathryn.Contracts.Reliability
{
    /// <summary>Receives structured operational diagnostic entries.</summary>
    public interface IOperationalLogSink
    {
        /// <summary>Writes a structured diagnostic entry.</summary>
        void Write(OperationalLogEntry entry);
    }
}
