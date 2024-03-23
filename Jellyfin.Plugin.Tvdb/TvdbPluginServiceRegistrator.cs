using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Register tvdb services.
    /// </summary>
    public class TvdbPluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<TvdbCultureInfo>();
            serviceCollection.AddSingleton<TvdbClientManager>();
        }
    }
}
