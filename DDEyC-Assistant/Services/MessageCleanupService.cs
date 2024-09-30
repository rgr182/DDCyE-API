
using DDEyC_Assistant.Repositories;

namespace DDEyC_Assistant.Services
{
    public class MessageCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<MessageCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval;
        private readonly TimeSpan _messageRetentionPeriod;

        public MessageCleanupService(
            IServiceProvider services,
            ILogger<MessageCleanupService> logger,
            IConfiguration configuration)
        {
            _services = services;
            _logger = logger;
            _cleanupInterval = TimeSpan.Parse(configuration["MessageCleanup:Interval"]);
            _messageRetentionPeriod = TimeSpan.Parse(configuration["MessageCleanup:RetentionPeriod"]);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Message cleanup task running at: {time}", DateTimeOffset.Now);

                using (var scope = _services.CreateScope())
                {
                    var chatRepository = scope.ServiceProvider.GetRequiredService<IChatRepository>();
                    await chatRepository.DeleteOldMessages(_messageRetentionPeriod);
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }
    }
}