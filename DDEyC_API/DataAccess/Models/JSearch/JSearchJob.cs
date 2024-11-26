using System.Text.Json.Serialization;

namespace DDEyC_API.Models.JSearch
{
    public class JSearchJob
    {
        public string job_id { get; set; }
        public string? employer_name { get; set; }
        public string? employer_website { get; set; }
        public string? job_employment_type { get; set; }
        public string job_title { get; set; } = string.Empty;
        public string? job_apply_link { get; set; }
        public string? job_description { get; set; }
        public bool job_is_remote { get; set; }
        public string? job_posted_at_datetime_utc { get; set; }
        public string? job_city { get; set; }
        public string? job_state { get; set; }
        public string? job_country { get; set; }
        public string? job_google_link { get; set; }
        public JSearchJobHighlights? job_highlights { get; set; }
        public string? job_naics_name { get; set; }
    }
}