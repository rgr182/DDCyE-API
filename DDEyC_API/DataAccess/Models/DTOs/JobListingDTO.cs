namespace DDEyC_API.Models.DTOs
{
    public class JobListingFilter
    {
        public string? Title { get; set; }
        public string? CompanyName { get; set; }
        public string? Location { get; set; }
        public string? Seniority { get; set; }
        public string? EmploymentType { get; set; }
        public List<string>? JobFunctions { get; set; }
        public List<string>? Industries { get; set; }
        public int Limit { get; set; } = 5; // Default limit
    }
}