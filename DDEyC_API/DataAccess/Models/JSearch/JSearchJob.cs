using System.Text.Json.Serialization;

namespace DDEyC_API.Models.JSearch
{
  public class JSearchJob
    {
        public string job_id { get; set; }
        public string employer_name { get; set; }= string.Empty;
        public string job_title { get; set; } = string.Empty;
        public string job_description { get; set; } = string.Empty;
        public string job_employment_type { get; set; } = string.Empty;
        public string job_apply_link { get; set; } = string.Empty;
        public bool job_is_remote { get; set; }
        public string job_posted_at_datetime_utc { get; set; } = string.Empty;
        // Preserve all location fields
        public string job_city { get; set; } = string.Empty;
        public string job_state { get; set; } = string.Empty;
        public string job_country { get; set; } = string.Empty;
        public JSearchJobHighlights? job_highlights { get; set; } 
    }
}