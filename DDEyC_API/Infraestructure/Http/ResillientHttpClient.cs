using DDEyC_API.Models.JSearch;
using Microsoft.Extensions.Options;
using Polly;
using Polly.RateLimit;
using Polly.Retry;
using Polly.Timeout;

namespace DDEyC_API.Infrastructure.Http
{
    public class ResilientHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncRateLimitPolicy _rateLimitPolicy;
        private readonly AsyncTimeoutPolicy _timeoutPolicy;
        private readonly ILogger<ResilientHttpClient> _logger;

        public ResilientHttpClient(
            HttpClient httpClient,
            IOptions<JSearchOptions> options,
            ILogger<ResilientHttpClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            var config = options.Value;

            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    config.RetryCount,
                    retryAttempt => TimeSpan.FromMilliseconds(
                        config.RetryDelayMilliseconds * Math.Pow(2, retryAttempt - 1)),
                    onRetry: (context, timeSpan, retryCount) =>
                    {
                        var exception = context.Exception;
                        logger.LogWarning(
                            new EventId(), // Pass a default EventId value
                            exception,
                            "Retry {RetryCount} after {DelayMs}ms due to {ExceptionType}",
                            retryCount,
                            timeSpan.TotalMilliseconds,
                            exception.GetType().Name);
                    });

            _rateLimitPolicy = Policy.RateLimitAsync(
                config.RateLimitPerMinute,
                TimeSpan.FromMinutes(1));

            _timeoutPolicy = Policy.TimeoutAsync(
                TimeSpan.FromSeconds(config.TimeoutSeconds));

            // Configure default headers
            _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Key", config.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Host", "jsearch.p.rapidapi.com");
        }

        public async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken = default)
        {
            return await _timeoutPolicy
                .WrapAsync(_retryPolicy)
                .WrapAsync(_rateLimitPolicy)
                .ExecuteAsync(async () => await _httpClient.SendAsync(request, cancellationToken));
        }
    }
}