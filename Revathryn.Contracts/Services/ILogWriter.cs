namespace Revathryn.Contracts.Services
{
    /// <summary>
    /// Interface for writing to log files
    /// </summary>
    public interface ILogWriter
    {
        /// <summary>
        /// Writes a single line
        /// </summary>
        /// <param name="message"></param>
        void WriteLine(string message);
    }
}
