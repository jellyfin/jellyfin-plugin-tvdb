#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA2227 // Collection properties should be read only

using System.Text.Json.Serialization;
using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb.SeasonClient
{
    public sealed class Response99
    {
        private System.Collections.Generic.IDictionary<string, object> _additionalProperties = default!;

        [JsonPropertyName("data")]
        public CustomSeasonExtendedRecord Data { get; set; } = default!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = default!;

        [JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }
    }
}
