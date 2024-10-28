using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DDEyC_Assistant.Models
{
    public class Message
{
    [Key]
    public int Id { get; set; }
    public int UserThreadId { get; set; }
    [ForeignKey("UserThreadId")]
    public UserThread UserThread { get; set; }
    public string Content { get; set; }
    /// <summary>
    /// String representation of OpenAI.Assistants.MessageRole enum.
    /// Common values: "User", "Assistant", "System"
    /// </summary>
    public string Role { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsFavorite { get; set; }
    public string FavoriteNote { get; set; }
}
}