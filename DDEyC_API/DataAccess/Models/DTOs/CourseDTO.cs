namespace DDEyC_API.DataAccess.Models.DTOs{
    public class CourseDto
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Description { get; set; }
    public string DetailLink { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
}