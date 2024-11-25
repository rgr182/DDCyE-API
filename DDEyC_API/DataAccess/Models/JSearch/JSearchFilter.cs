namespace DDEyC_API.Models.JSearch
{
 public class JSearchFilter
    {
        public string? Query { get; set; }
        public int Page { get; set; } = 1;
        public int NumPages { get; set; } = 1;
        public string DatePosted { get; set; } = "all"; // all, today, 3days, week, month
    }
}