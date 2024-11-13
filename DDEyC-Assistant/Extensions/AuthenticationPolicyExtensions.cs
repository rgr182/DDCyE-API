using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DDEyC_Assistant.Options;
using DDEyC_Assistant.Policies;
using System.Net.Http;

namespace DDEyC_Assistant.Extensions
{
    public static class AuthenticationPolicyExtensions
    {
        public static IServiceCollection AddAuthenticationPolicy(
            this IServiceCollection services,
            AuthenticationPolicyOptions options)
        {
            services.Configure<AuthenticationPolicyOptions>(opt =>
            {
                opt.MaxRetryAttempts = options.MaxRetryAttempts;
                opt.InitialRetryDelaySeconds = options.InitialRetryDelaySeconds;
                opt.UseExponentialBackoff = options.UseExponentialBackoff;
                opt.HttpClientTimeout = options.HttpClientTimeout;
                opt.HttpClientLifetime = options.HttpClientLifetime;
            });
            
            services.AddSingleton<IAuthenticationPolicy, AuthenticationPolicy>();
            
            services.AddHttpClient("AuthClient")
                .ConfigureHttpClient(client =>
                {
                    client.Timeout = options.HttpClientTimeout;
                })
                .SetHandlerLifetime(options.HttpClientLifetime);

            return services;
        }
    }
}