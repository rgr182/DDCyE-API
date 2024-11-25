namespace DDEyC_API.Models.JSearch{
     public class JSearchResponse
    {
        public string status { get; set; }
        public string request_id { get; set; }
        public JSearchParameters parameters { get; set; }
        public List<JSearchJob> data { get; set; }
    }
}