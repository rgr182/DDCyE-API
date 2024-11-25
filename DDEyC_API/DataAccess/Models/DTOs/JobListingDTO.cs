namespace DDEyC_API.Models.DTOs
{
    public class JobListingFilter
    {
        public string? Title { get; set; }
        public string? CompanyName { get; set; }
        public string? Location { get; set; }
        public string? EmploymentType { get; set; }
        public int Limit { get; set; } = 10;
        public string? DatePosted { get; set; } // Mapped to JSearch date_posted
        public bool? IsRemote { get; set; }
        public decimal? MinSalary { get; set; }
        public string? RequiredExperience { get; set; }
    }
}