using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DDEyC_Assistant.Models
{
    public class UserThread
{
    [Key]
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ThreadId { get; set; }
    public DateTime LastUsed { get; set; }
    public bool IsActive { get; set; }
    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; set; }
    public string? FavoriteNote { get; set; }
}
}