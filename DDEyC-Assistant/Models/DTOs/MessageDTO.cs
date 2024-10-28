
using System.Text.Json.Serialization;

namespace DDEyC_Assistant.Models.DTOs{
public class MessageDto
{
    public int Id { get; set; }
    public string Content { get; set; }
    public string Role { get; set; }
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    public bool IsFavorite { get; set; }
    public string FavoriteNote { get; set; }
}
 }
     