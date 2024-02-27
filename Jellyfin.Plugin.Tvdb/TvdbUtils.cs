using System;
using System.Collections.Generic;

using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb utils.
    /// </summary>
    public static class TvdbUtils
    {
        /// <summary>
        /// Base url for all requests.
        /// </summary>
        public const string TvdbBaseUrl = "https://www.thetvdb.com/";

        /// <summary>
        /// Converts SeriesAirsDays to DayOfWeek array.
        /// </summary>
        /// <param name="seriesAirsDays">SeriesAirDays.</param>
        /// <returns>List{DayOfWeek}.</returns>
        public static IEnumerable<DayOfWeek> GetAirDays(SeriesAirsDays seriesAirsDays)
        {
            if (seriesAirsDays.Sunday)
            {
                yield return DayOfWeek.Sunday;
            }

            if (seriesAirsDays.Monday)
            {
                yield return DayOfWeek.Monday;
            }

            if (seriesAirsDays.Tuesday)
            {
                yield return DayOfWeek.Tuesday;
            }

            if (seriesAirsDays.Wednesday)
            {
                yield return DayOfWeek.Wednesday;
            }

            if (seriesAirsDays.Thursday)
            {
                yield return DayOfWeek.Thursday;
            }

            if (seriesAirsDays.Friday)
            {
                yield return DayOfWeek.Friday;
            }

            if (seriesAirsDays.Saturday)
            {
                yield return DayOfWeek.Saturday;
            }
        }
    }
}
