using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Tvdb.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb Plugin.
    /// </summary>
    public class TvdbPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public const string ProviderName = "TheTVDB";

        /// <summary>
        /// Gets the provider id.
        /// </summary>
        public const string ProviderId = "Tvdb";

        private readonly ILogger<TvdbPlugin> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbPlugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        public TvdbPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<TvdbPlugin> logger)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            _logger = logger;
            var path = Path.Join(applicationPaths.WebPath, "index.html");
            try
            {
                InjectDisplayOrderOptions(path);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to inject display order options script.");
            }
        }

        /// <summary>
        /// Gets current plugin instance.
        /// </summary>
        public static TvdbPlugin? Instance { get; private set; }

        /// <inheritdoc />
        public override string Name => "TheTVDB";

        /// <inheritdoc />
        public override Guid Id => new Guid("a677c0da-fac5-4cde-941a-7134223f14c8");

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.config.html"
                },
                new PluginPageInfo
                {
                    Name = "more-display-order-options.js",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.more-display-order-options.js"
                }
            };
        }

        private void InjectDisplayOrderOptions(string path)
        {
            var content = File.ReadAllText(path);

            var script = "<script src=\"configurationpage?name=more-display-order-options.js\"></script>";
            if (content.Contains(script, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Display order options script already injected.");
                return;
            }

            var headEnd = new Regex("</head>", RegexOptions.IgnoreCase);
            content = headEnd.Replace(content, script + "</head>", 1);
            File.WriteAllText(path, content);
            _logger.LogInformation("Display order options script injected.");
        }
    }
}
