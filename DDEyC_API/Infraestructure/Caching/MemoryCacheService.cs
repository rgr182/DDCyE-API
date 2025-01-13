using Microsoft.Extensions.Caching.Memory;

namespace DDEyC_API.Infrastructure.Caching
{
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryCacheService> _logger;

        public MemoryCacheService(
            IMemoryCache cache,
            ILogger<MemoryCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var value = _cache.Get<T>(key);
                if (value != null)
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                }
                else
                {
                    _logger.LogDebug("Cache miss for key: {Key}", key);
                }
                return Task.FromResult(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving value from cache for key: {Key}", key);
                return Task.FromResult<T?>(default);
            }
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            try
            {
                var options = new MemoryCacheEntryOptions();
                
                if (expiration.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiration;
                }
                else
                {
                    // Default expiration of 30 minutes if none specified
                    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                }

                // Add sliding expiration to keep frequently accessed items
                options.SlidingExpiration = TimeSpan.FromMinutes(10);
                
                // Set size limits if needed
                options.Size = 1; // Each entry counts as 1 unit

                // Add eviction callback for logging
                options.RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    _logger.LogDebug(
                        "Cache entry {Key} was evicted due to {Reason}",
                        key,
                        reason);
                });

                _cache.Set(key, value, options);
                _logger.LogDebug("Successfully cached value for key: {Key}", key);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
                return Task.CompletedTask;
            }
        }

        public Task RemoveAsync(string key)
        {
            try
            {
                _cache.Remove(key);
                _logger.LogDebug("Successfully removed key from cache: {Key}", key);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing key from cache: {Key}", key);
                return Task.CompletedTask;
            }
        }
    }
}