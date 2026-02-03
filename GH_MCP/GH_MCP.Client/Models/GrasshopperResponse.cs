using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GH_MCP.Client.Models
{
    /// <summary>
    /// Represents a response from the Grasshopper MCP plugin
    /// </summary>
    public class GrasshopperResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data")]
        public object? Data { get; set; }

        [JsonProperty("result")]
        public object? Result { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Gets the result data, checking both 'data' and 'result' fields
        /// </summary>
        public object? GetResultData()
        {
            return Data ?? Result;
        }

        /// <summary>
        /// Gets the result as a specific type
        /// </summary>
        public T? GetResultAs<T>() where T : class
        {
            var result = GetResultData();
            if (result == null) return null;

            if (result is T typed)
                return typed;

            if (result is JObject jObj)
                return jObj.ToObject<T>();

            if (result is JArray jArr)
                return jArr.ToObject<T>();

            return null;
        }

        /// <summary>
        /// Gets a property from the result data
        /// </summary>
        public T? GetProperty<T>(string propertyName)
        {
            var result = GetResultData();
            if (result == null) return default;

            if (result is JObject jObj && jObj.TryGetValue(propertyName, out var token))
            {
                return token.ToObject<T>();
            }

            return default;
        }

        /// <summary>
        /// Creates a successful response
        /// </summary>
        public static GrasshopperResponse Ok(object? data = null)
        {
            return new GrasshopperResponse
            {
                Success = true,
                Data = data
            };
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        public static GrasshopperResponse CreateError(string errorMessage)
        {
            return new GrasshopperResponse
            {
                Success = false,
                Error = errorMessage
            };
        }

        /// <summary>
        /// Serializes the response to JSON string
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Deserializes a response from JSON string
        /// </summary>
        public static GrasshopperResponse? FromJson(string json)
        {
            return JsonConvert.DeserializeObject<GrasshopperResponse>(json);
        }
    }
}
