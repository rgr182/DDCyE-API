using DDEyC_API.Infrastructure.Caching;
using DDEyC_API.Infrastructure.Http;
using DDEyC_API.Models.JSearch;
using DDEyC_API.Services.JSearch;

namespace DDEyC_API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCachingServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add Memory Cache with custom size limit
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = 1024; // Limit cache to 1024 entries
            });

            // Register cache service
            services.AddSingleton<ICacheService, MemoryCacheService>();

            return services;
        }

        public static IServiceCollection AddJSearchServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure options
            // services.Configure<JSearchOptions>(
            //     configuration.GetSection("JSearch"));
            services.AddSingleton<JSearchOptions>(configuration.GetSection("JSearch").Get<JSearchOptions>());
            // Add caching
            services.AddCachingServices(configuration);

            // Register services
            services.AddHttpClient();
            services.AddScoped<ResilientHttpClient>();
            services.AddScoped<IJSearchService, JSearchService>();

            return services;
        }
    }
}