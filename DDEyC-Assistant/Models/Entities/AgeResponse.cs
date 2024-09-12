using System.Text.Json.Serialization;

namespace DDEyC_Assistant.Models.Entities
{
    public class AgeResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }
    }
}