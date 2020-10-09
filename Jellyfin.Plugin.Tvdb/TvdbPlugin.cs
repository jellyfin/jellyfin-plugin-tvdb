using System;
using Jellyfin.Plugin.Tvdb.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb Plugin.
    /// </summary>
    public class TvdbPlugin : BasePlugin<PluginConfiguration>/*, IHasWebPages*/
    {
        /// <summary>
        /// Gets the provider id.
        /// </summary>
        public const string ProviderName = "Tvdb";

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbPlugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        public TvdbPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets current plugin instance.
        /// </summary>
        public static TvdbPlugin? Instance { get; private set; }

        /// <inheritdoc />
        public override string Name => "TheTVDB";

        /// <inheritdoc />
        public override Guid Id => new Guid("a677c0da-fac5-4cde-941a-7134223f14c8");

        /*/// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.config.html"
            };
        }*/
    }
}