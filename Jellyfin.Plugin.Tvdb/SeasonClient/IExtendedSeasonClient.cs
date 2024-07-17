using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Tvdb.SeasonClient
{
    /// <summary>
    /// Interface IExtendedSeasonClient.
    /// </summary>
    public interface IExtendedSeasonClient
    {
        /// <summary>
        /// Gets the season extended with translations.
        /// </summary>
        /// <param name="id">Season Id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>response.</returns>
        System.Threading.Tasks.Task<Response99> GetSeasonExtendedWithTranslationsAsync(double id, System.Threading.CancellationToken cancellationToken = default);
    }
}
