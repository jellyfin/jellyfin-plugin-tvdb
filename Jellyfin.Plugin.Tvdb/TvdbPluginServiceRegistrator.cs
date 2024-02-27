using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;

using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Tvdb;

/// <summary>
/// Register tvdb services.
/// </summary>
public class TvdbPluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<TvdbClientManager>();
    }
}
