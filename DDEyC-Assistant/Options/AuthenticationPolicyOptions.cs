namespace DDEyC_Assistant.Options
{
    public class AuthenticationPolicyOptions
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public double InitialRetryDelaySeconds { get; set; } = 2;
        public bool UseExponentialBackoff { get; set; } = true;
        public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan HttpClientLifetime { get; set; } = TimeSpan.FromMinutes(5);
    }
}