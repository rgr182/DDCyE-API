namespace DDEyC_API.Models.JSearch
{
    public record JSearchResponse
    {
        public List<JSearchJob> Data { get; init; } = new();
        public JSearchMetadata? Metadata { get; init; }
    }

    public record JSearchMetadata
    {
        public int TotalCount { get; init; }
        public int PageCount { get; init; }
        public string? NextCursor { get; init; }
    }

    public record JSearchJob
    {
        public string JobId { get; init; } = string.Empty;
        public string JobTitle { get; init; } = string.Empty;
        public string JobDescription { get; init; } = string.Empty;
        public string JobCity { get; init; } = string.Empty;
        public string JobCountry { get; init; } = string.Empty;
        public string JobEmploymentType { get; init; } = string.Empty;
        public string JobApplyLink { get; init; } = string.Empty;
        public DateTime JobPostedAtDateTime { get; init; }
        public string EmployerName { get; init; } = string.Empty;
        public string? EmployerWebsite { get; init; }
        public decimal? JobMinSalary { get; init; }
        public decimal? JobMaxSalary { get; init; }
        public string? JobSalaryCurrency { get; init; }
        public JSearchJobHighlights? JobHighlights { get; init; }
    }
    public record JSearchJobHighlights
    {
        public List<string> Qualifications { get; init; } = new();
        public List<string> Responsibilities { get; init; } = new();
        public List<string> Benefits { get; init; } = new();
    }
      public class JSearchFilter
    {
        public string? Query { get; set; }
        public int Page { get; set; } = 1;
        public int NumPages { get; set; } = 1;
        public string DatePosted { get; set; } = "all"; // all, today, 3days, week, month
    }
}