namespace DDEyC_API.Models.JSearch
{
    public class JSearchOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://jsearch.p.rapidapi.com";
        public int CacheExpirationMinutes { get; set; } = 30;
        public int RetryCount { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
        public int TimeoutSeconds { get; set; } = 30;
        public int RateLimitPerMinute { get; set; } = 50;
    }
}