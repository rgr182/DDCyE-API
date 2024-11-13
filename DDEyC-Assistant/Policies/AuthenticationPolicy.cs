using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using System.Net.Sockets;
using DDEyC_Assistant.Options;

namespace DDEyC_Assistant.Policies
{
    public class AuthenticationPolicy : IAuthenticationPolicy
    {
        private readonly ILogger<AuthenticationPolicy> _logger;
        private readonly AuthenticationPolicyOptions _options;

        public AuthenticationPolicy(
            ILogger<AuthenticationPolicy> logger,
            IOptions<AuthenticationPolicyOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            RetryPolicy = CreateRetryPolicy();
        }

        public IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; }

        private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
        {
            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(
                    _options.MaxRetryAttempts,
                    retryAttempt => CalculateRetryDelay(retryAttempt),
                    (delegateResult, duration, retryCount, context) =>
                    {
                        if (delegateResult.Exception != null)
                        {
                            _logger.LogWarning(
                                "Retry attempt {RetryCount} of {MaxRetries} failed. Waiting {Duration} before next retry. Error: {Error}",
                                retryCount,
                                _options.MaxRetryAttempts,
                                duration,
                                delegateResult.Exception.Message);
                        }
                    });
        }

        private TimeSpan CalculateRetryDelay(int retryAttempt)
        {
            if (_options.UseExponentialBackoff)
            {
                return TimeSpan.FromSeconds(
                    _options.InitialRetryDelaySeconds * Math.Pow(2, retryAttempt - 1));
            }
            
            return TimeSpan.FromSeconds(_options.InitialRetryDelaySeconds);
        }
    }
}