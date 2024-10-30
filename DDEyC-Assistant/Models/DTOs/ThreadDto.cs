namespace DDEyC_Assistant.Models.DTOs{
    public class UserThreadDto
{
    public int Id { get; set; }
    public string ThreadId { get; set; }
    public DateTime LastUsed { get; set; }
    public bool IsActive { get; set; }
    public bool IsFavorite { get; set; }
    public string? FavoriteNote { get; set; }
}
}