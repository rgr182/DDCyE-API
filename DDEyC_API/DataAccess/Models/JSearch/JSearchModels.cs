using System.Text.Json.Serialization;

namespace DDEyC_API.Models.JSearch
{
    public class JSearchResponse
    {
        public string status { get; set; }
        public string request_id { get; set; }
        public JSearchParameters parameters { get; set; }
        public List<JSearchJob> data { get; set; }
    }

    public class JSearchParameters
    {
        public string query { get; set; }
        public int page { get; set; }
        public int num_pages { get; set; }
        public string date_posted { get; set; }
    }


    public class JSearchJob
    {
        public string job_id { get; set; }
        public string employer_name { get; set; }
        public string employer_website { get; set; }
        public string job_employment_type { get; set; }
        public string job_title { get; set; }
        public string job_apply_link { get; set; }
        public string job_description { get; set; }
        public bool job_is_remote { get; set; }
        public string job_posted_at_datetime_utc { get; set; }
        public string job_city { get; set; }
        public string job_state { get; set; }
        public string job_country { get; set; }
        public string job_google_link { get; set; }
        public JSearchJobHighlights job_highlights { get; set; }
        public string job_naics_name { get; set; }
    }

    public class JSearchJobHighlights
    {
        public List<string> Qualifications { get; set; } = new();
        public List<string> Responsibilities { get; set; } = new();
        public List<string> Benefits { get; set; } = new();
    }
    public class JSearchFilter
    {
        public string? Query { get; set; }
        public int Page { get; set; } = 1;
        public int NumPages { get; set; } = 1;
        public string DatePosted { get; set; } = "all"; // all, today, 3days, week, month
    }
}