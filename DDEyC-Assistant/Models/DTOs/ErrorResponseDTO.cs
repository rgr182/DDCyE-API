namespace DDEyC_Assistant.Models.DTOs
{
   public class ErrorResponse
    {
        public string Message { get; set; }
        public string ErrorCode { get; set; }
        public Dictionary<string, string[]> ValidationErrors { get; set; }
    }
}