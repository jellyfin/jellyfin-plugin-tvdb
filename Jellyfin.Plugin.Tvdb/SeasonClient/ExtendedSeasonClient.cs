#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods

using System.Text.Json.Serialization;
using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb.SeasonClient
{
    /// <summary>
    /// Extended season client.
    /// </summary>
    public sealed partial class ExtendedSeasonClient : SeasonsClient, IExtendedSeasonClient
    {
        private System.Net.Http.HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtendedSeasonClient"/> class.
        /// </summary>
        /// <param name="configuration">Instance of <see cref="SdkClientSettings"/>.</param>
        /// <param name="httpClient">Instance of <see cref="System.Net.Http.HttpClient"/>.</param>
        public ExtendedSeasonClient(SdkClientSettings configuration, System.Net.Http.HttpClient httpClient) : base(configuration, httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc/>
        public async System.Threading.Tasks.Task<Response99> GetSeasonExtendedWithTranslationsAsync(double id, System.Threading.CancellationToken cancellationToken = default)
        {
            var client_ = _httpClient;
            var disposeClient_ = false;
            try
            {
                using (var request_ = new System.Net.Http.HttpRequestMessage())
                {
                    request_.Method = new System.Net.Http.HttpMethod("GET");
                    request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

                    var urlBuilder_ = new System.Text.StringBuilder();

                    // Operation Path: "seasons/{id}/extended"
                    urlBuilder_.Append("seasons/");
                    urlBuilder_.Append(System.Uri.EscapeDataString(ConvertToString(id, System.Globalization.CultureInfo.InvariantCulture)));
                    urlBuilder_.Append("/extended?meta=translations");

                    await PrepareRequestAsync(client_, request_, urlBuilder_, cancellationToken).ConfigureAwait(false);

                    var url_ = urlBuilder_.ToString();
                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

                    await PrepareRequestAsync(client_, request_, url_, cancellationToken).ConfigureAwait(false);

                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    var disposeResponse_ = true;
                    try
                    {
                        var headers_ = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>>();
                        foreach (var item_ in response_.Headers)
                        {
                            headers_[item_.Key] = item_.Value;
                        }

                        if (response_.Content != null && response_.Content.Headers != null)
                        {
                            foreach (var item_ in response_.Content.Headers)
                            {
                                headers_[item_.Key] = item_.Value;
                            }
                        }

                        await ProcessResponseAsync(client_, response_, cancellationToken).ConfigureAwait(false);

                        var status_ = (int)response_.StatusCode;
                        if (status_ == 200)
                        {
                            var objectResponse_ = await ReadObjectResponseAsync<Response99>(response_, headers_, cancellationToken).ConfigureAwait(false);
                            if (objectResponse_.Object == null)
                            {
                                throw new SeasonsException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
                            }

                            return objectResponse_.Object;
                        }
                        else
                        if (status_ == 400)
                        {
                            var responseText_ = response_.Content == null ? string.Empty : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new SeasonsException("Invalid seasons id", status_, responseText_, headers_, null);
                        }
                        else
                        if (status_ == 401)
                        {
                            var responseText_ = response_.Content == null ? string.Empty : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new SeasonsException("Unauthorized", status_, responseText_, headers_, null);
                        }
                        else
                        if (status_ == 404)
                        {
                            var responseText_ = response_.Content == null ? string.Empty : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new SeasonsException("Season not found", status_, responseText_, headers_, null);
                        }
                        else
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            throw new SeasonsException("The HTTP status code of the response was not expected (" + status_ + ").", status_, responseData_, headers_, null);
                        }
                    }
                    finally
                    {
                        if (disposeResponse_)
                        {
                            response_.Dispose();
                        }
                    }
                }
            }
            finally
            {
                if (disposeClient_)
                {
                    client_.Dispose();
                }
            }
        }

        private string ConvertToString(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is System.Enum)
            {
                var name = System.Enum.GetName(value.GetType(), value);
                if (name != null)
                {
                    var field = System.Reflection.IntrospectionExtensions.GetTypeInfo(value.GetType()).GetDeclaredField(name);
                    if (field != null)
                    {
                        var attribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Runtime.Serialization.EnumMemberAttribute>(field);
                        if (attribute != null)
                        {
                            return attribute.Value != null ? attribute.Value : name;
                        }
                    }

                    var converted = System.Convert.ToString(System.Convert.ChangeType(value, System.Enum.GetUnderlyingType(value.GetType()), cultureInfo), cultureInfo);
                    return converted == null ? string.Empty : converted;
                }
            }
            else if (value is bool)
            {
                return System.Convert.ToString((bool)value, cultureInfo).ToLowerInvariant();
            }
            else if (value is byte[])
            {
                return System.Convert.ToBase64String((byte[])value);
            }
            else if (value is string[])
            {
                return string.Join(",", (string[])value);
            }
            else if (value.GetType().IsArray)
            {
                var valueArray = (System.Array)value;
                var valueTextArray = new string[valueArray.Length];
                for (var i = 0; i < valueArray.Length; i++)
                {
                    valueTextArray[i] = ConvertToString(valueArray.GetValue(i)!, cultureInfo);
                }

                return string.Join(",", valueTextArray);
            }

            var result = System.Convert.ToString(value, cultureInfo);
            return result == null ? string.Empty : result;
        }
    }
}
