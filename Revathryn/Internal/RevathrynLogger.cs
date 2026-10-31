using Revathryn.Contracts.Services;

namespace Revathryn.Internal
{
    internal sealed class RevathrynLogger : PluginManager.Abstractions.ILogger
    {
        private sealed class TraceLogWriter : ILogWriter
        {
            public void WriteLine(string message) => System.Diagnostics.Trace.WriteLine(message);
        }

        private readonly ILogWriter _logWriter;

        /// <summary>
        /// 
        /// </summary>
        public RevathrynLogger() : this(new TraceLogWriter())
        {
        }

        /// <summary>
        /// Constructs a new instance of <see cref="RevathrynLogger"/>.
        /// </summary>
        public RevathrynLogger(ILogWriter logWriter)
        {
            _logWriter = logWriter;
        }

        public void AddToLog(in PluginManager.LogLevel logLevel, in string data)
        {
            AddToLog(logLevel, String.Empty, data);
        }

        public void AddToLog(in PluginManager.LogLevel logLevel, in Exception exception)
        {
            AddToLog(logLevel, String.Empty, exception, String.Empty);
        }

        public void AddToLog(in PluginManager.LogLevel logLevel, in Exception exception, string data)
        {
            AddToLog(logLevel, String.Empty, exception, data);
        }

        public void AddToLog(in PluginManager.LogLevel logLevel, in string moduleName, in string data)
        {
            _logWriter?.WriteLine($"[{logLevel}] [{moduleName}] {data}");
        }

        public void AddToLog(in PluginManager.LogLevel logLevel, in string moduleName, in Exception exception)
        {
            AddToLog(logLevel, moduleName, exception, String.Empty);
        }

        public void AddToLog(in PluginManager.LogLevel logLevel, in string moduleName, in Exception exception, string data)
        {
            _logWriter?.WriteLine($"[{logLevel}] [{moduleName}] {data}");

            if (exception != null)
            {
                _logWriter?.WriteLine(exception.ToString());
            }
        }
    }
}
