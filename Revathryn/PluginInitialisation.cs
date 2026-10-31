using PluginManager.Abstractions;

namespace Revathryn
{
    public class PluginInitialisation : IPlugin
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // not used in this context
        }

        public void Finalise()
        {
            // not used in this context
        }

        public ushort GetVersion()
        {
            return 1;
        }

        public void Initialise(PluginManager.Abstractions.ILogger logger)
        {
            // not used in this context
        }
    }
}
