namespace DDEyC_API.Models.DTOs
{
    public class CourseFilter
    {
        public string SearchTerm { get; set; } = string.Empty;
        public string? Location { get; set; }
        public int Limit { get; set; } = 5;
    }
}
