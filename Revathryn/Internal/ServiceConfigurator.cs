using Microsoft.Extensions.DependencyInjection.Extensions;

using PluginManager.Abstractions;

namespace Revathryn.Internal
{
    /// <summary>
    /// Final service configurator
    /// </summary>
    internal sealed class ServiceConfigurator : IServiceConfigurator
    {
        /// <summary>
        /// Registers services with the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"></param>
        public void RegisterServices(IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Singleton<PluginManager.Abstractions.ILogger, RevathrynLogger>());
        }
    }
}
