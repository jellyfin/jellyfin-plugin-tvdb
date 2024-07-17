#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA2227 // Collection properties should be read only

using System.Text.Json.Serialization;
using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb.SeasonClient
{
    public sealed class CustomSeasonExtendedRecord
    {
        private System.Collections.Generic.IDictionary<string, object> _additionalProperties = default!;

        [JsonPropertyName("artwork")]
        public System.Collections.Generic.IReadOnlyList<ArtworkBaseRecord> Artwork { get; set; } = default!;

        [JsonPropertyName("companies")]
        public Companies Companies { get; set; } = default!;

        [JsonPropertyName("episodes")]
        public System.Collections.Generic.IReadOnlyList<EpisodeBaseRecord> Episodes { get; set; } = default!;

        [JsonPropertyName("id")]
        public int? Id { get; set; } = default!;

        [JsonPropertyName("image")]
        public string Image { get; set; } = default!;

        [JsonPropertyName("imageType")]
        public int? ImageType { get; set; }

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; } = default!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("nameTranslations")]
        public System.Collections.Generic.IReadOnlyList<string> NameTranslations { get; set; } = default!;

        [JsonPropertyName("number")]
        public long? Number { get; set; }

        [JsonPropertyName("overviewTranslations")]
        public System.Collections.Generic.IReadOnlyList<string> OverviewTranslations { get; set; } = default!;

        [JsonPropertyName("seriesId")]
        public long? SeriesId { get; set; }

        [JsonPropertyName("trailers")]
        public System.Collections.Generic.IReadOnlyList<Trailer> Trailers { get; set; } = default!;

        [JsonPropertyName("type")]
        public SeasonType Type { get; set; } = default!;

        [JsonPropertyName("tagOptions")]
        public System.Collections.Generic.IReadOnlyList<TagOption> TagOptions { get; set; } = default!;

        [JsonPropertyName("translations")]
        public TranslationExtended Translations { get; set; } = default!;

        [JsonPropertyName("year")]
        public string Year { get; set; } = default!;

        [JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }
    }
}
