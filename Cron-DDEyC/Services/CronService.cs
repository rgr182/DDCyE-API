using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using NCrontab;
using Cron_BolsaDeTrabajo.Infrastructure;

namespace Cron_BolsaDeTrabajo.Services
{
    // Interface for Cron Service
    public interface ICronService
    {
        Task StartAsync();
    }

    // Implementation of Cron Service
    public class CronService : ICronService
    {
        private readonly IMongoDbConnection _mongoDbConnection;
        private readonly IApiService _apiService;
        private readonly IMongoCollection<BsonDocument> _configurationCollection;
        private readonly Timer _timer;
        private readonly string _cronExpression;
        private readonly IConfiguration _configuration;

        public CronService(IMongoDbConnection mongoDbConnection, IApiService apiService, IConfiguration configuration)
        {
            _mongoDbConnection = mongoDbConnection;
            _apiService = apiService;
            _configuration = configuration;

            // Setup MongoDB collection access
            var mongoCollectionName = _configuration["MongoDB:CollectionName"];
            _configurationCollection = _mongoDbConnection.GetCollection<BsonDocument>(mongoCollectionName);

            // Load cron expression from configuration
            _cronExpression = _configuration["CronJob:CronExpression"];

            if (string.IsNullOrEmpty(_cronExpression))
            {
                Console.WriteLine("No valid cron expression found in the configuration.");
                return;
            }

            // Initialize timer based on the cron expression
            TimeSpan timeUntilNextRun = CalculateTimeUntilNextRun(_cronExpression);
            _timer = new Timer(async _ => await ExecuteTaskAsync(), null, timeUntilNextRun, Timeout.InfiniteTimeSpan);
        }

        public async Task StartAsync()
        {
            // Initialize the cron job
            Console.WriteLine("Cron job started.");
        }

        private async Task ExecuteTaskAsync()
        {
            Console.WriteLine($"Executing task at {DateTime.Now}");

            // HTTP call to the API using the service
            string url = _configuration["Api:BaseUrl"];
            string responseBody = await _apiService.CallApiAsync(url);

            if (responseBody != null)
            {
                Console.WriteLine($"API Response: {responseBody}");
            }

            // Reset the timer for the next run according to the cron expression
            TimeSpan timeUntilNextRun = CalculateTimeUntilNextRun(_cronExpression);
            _timer.Change(timeUntilNextRun, Timeout.InfiniteTimeSpan);
        }

        private TimeSpan CalculateTimeUntilNextRun(string cronExpression)
        {
            var cronSchedule = CrontabSchedule.Parse(cronExpression);
            DateTime now = DateTime.Now;
            DateTime nextRun = cronSchedule.GetNextOccurrence(now);

            return nextRun - now;
        }
    }
}
