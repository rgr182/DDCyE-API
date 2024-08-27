using NCrontab;
using Cron_DDEyC.Services;
using Cron_DDEyC.Infraestructure;
namespace Cron_DDEyC
{
    class Program
    {
        private static Timer _timer;
        private static readonly ApiService _apiService = new ApiService();

        // Configuration details moved to a single place in MongoDbConnection class
        private static readonly MongoDbConnection _mongoDbConnection = new MongoDbConnection("YourMongoDBConnectionStringHere", "YourDatabaseName"); // Replace with your MongoDB connection details
        private static readonly CronService _cronService = new CronService(_mongoDbConnection, "YourCollectionName"); // Replace with your MongoDB collection name

        static async Task Main(string[] args)
        {
            Console.WriteLine("Cron Job Application started.");

            // Get the cron expression from MongoDB
            string cronExpression = await _cronService.GetCronExpressionAsync();
            if (string.IsNullOrEmpty(cronExpression))
            {
                Console.WriteLine("No valid cron expression found in the database.");
                return;
            }

            // Calculate the time until the next execution using the cron expression
            TimeSpan timeUntilNextRun = CalculateTimeUntilNextRun(cronExpression);
            _timer = new Timer(async _ => await ExecuteTaskAsync(cronExpression), null, timeUntilNextRun, Timeout.InfiniteTimeSpan);

            // Keep the application running
            Console.ReadLine();
        }

        private static async Task ExecuteTaskAsync(string cronExpression)
        {
            Console.WriteLine($"Executing task at {DateTime.Now}");

            // HTTP call to the API using the service
            string url = "https://api.your-api.com/endpoint"; // Replace with your API URL
            string responseBody = await _apiService.CallApiAsync(url);

            if (responseBody != null)
            {
                Console.WriteLine($"API Response: {responseBody}");
            }

            // Reset the timer for the next run according to the cron expression
            TimeSpan timeUntilNextRun = CalculateTimeUntilNextRun(cronExpression);
            _timer.Change(timeUntilNextRun, Timeout.InfiniteTimeSpan);
        }

        private static TimeSpan CalculateTimeUntilNextRun(string cronExpression)
        {
            var cronSchedule = CrontabSchedule.Parse(cronExpression);
            DateTime now = DateTime.Now;
            DateTime nextRun = cronSchedule.GetNextOccurrence(now);

            return nextRun - now;
        }
    }
}
