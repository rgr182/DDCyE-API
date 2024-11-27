namespace DDEyC_API.Models.JSearch
{
    public class JSearchParameters
    {
        public string query { get; set; } = string.Empty;
        public int page { get; set; }
        public int num_pages { get; set; }
        public string date_posted { get; set; } = "all";
    }
}