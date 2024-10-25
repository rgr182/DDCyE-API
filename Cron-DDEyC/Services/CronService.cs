using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Cron_BolsaDeTrabajo.Infrastructure;
using Cron_DDEyC.Utils; // Import the namespace where LongEqualityComparer is defined
using NCrontab;

namespace Cron_BolsaDeTrabajo.Services
{
    public interface ICronService
    {
        Task StartAsync();        
        Task ExecuteTaskAsync();
    }

    public class CronService : ICronService
    {
        private readonly IMongoDbConnection _mongoDbConnection;
        private readonly IApiService _apiService;
        private readonly ILinkedInJobService _linkedInJobService; // Service to fetch LinkedIn job IDs
        private readonly IMongoCollection<BsonDocument> _ofertasLaboralesCollection;
        private Timer _timer;
        private readonly string _cronExpression;
        private readonly IConfiguration _configuration;

        public CronService(IMongoDbConnection mongoDbConnection, IApiService apiService, IConfiguration configuration, ILinkedInJobService linkedInJobService)
        {
            _mongoDbConnection = mongoDbConnection;
            _apiService = apiService;
            _configuration = configuration;
            _linkedInJobService = linkedInJobService;

            // Setup MongoDB collection access
            var mongoCollectionName = _configuration["MongoDB:CollectionName"];
            _ofertasLaboralesCollection = _mongoDbConnection.GetCollection<BsonDocument>(mongoCollectionName);

            // Load cron expression from configuration
            _cronExpression = _configuration["CronJob:CronExpression"];

            if (string.IsNullOrEmpty(_cronExpression))
            {
                Console.WriteLine("No valid cron expression found in the configuration.");
                return;
            }

#if !TESTING
            // In testing mode, directly run the test method
            InitializeCronJob();
#endif
        }

        private void InitializeCronJob()
        {
            // Initialize timer based on the cron expression
            TimeSpan timeUntilNextRun = CalculateTimeUntilNextRun(_cronExpression);
            _timer = new Timer(async _ => await ExecuteTaskAsync(), null, timeUntilNextRun, Timeout.InfiniteTimeSpan);
            Console.WriteLine("Cron job initialized.");
        }

        public async Task StartAsync()
        {
            Console.WriteLine("Cron job started.");
            // Further implementation to start the cron job can be added here
        }        

        public async Task ExecuteTaskAsync()
        {
            Console.WriteLine($"Executing task at {DateTime.Now}");

            try
            {
                // Fetch job IDs from MongoDB (existing IDs as strings)
                var jobIdsFromDBString = await _linkedInJobService.GetLinkedInJobIdsFromDBAsync();

                // Convert string IDs to long IDs
                var jobIdsFromDB = jobIdsFromDBString
                    .Where(id => long.TryParse(id, out _)) // Ensure valid long values
                    .Select(long.Parse)
                    .ToList();

                // Fetch new job IDs from the API (as long)
                var jobIdsFromAPI = await _apiService.FetchJobIdsAsync();
                int maxCollects = int.Parse(_configuration["Api:MaxCollects"]);

                // Use the extension method to find new job IDs that are not in the database
                var newJobIds = jobIdsFromAPI.GetNewElements(jobIdsFromDB);

                // Loop through the new job IDs and fetch details for each
                for (int i = 0; i < Math.Min(newJobIds.Count, maxCollects); i++)
                {
                    long jobId = newJobIds[i];
                    string jobDetails = await _apiService.FetchJobDetailsAsync(jobId);

                    if (jobDetails != null)
                    {
                        Console.WriteLine($"Job ID {jobId} details fetched, saving to MongoDB...");
                        var document = BsonDocument.Parse(jobDetails);

                        try
                        {
                            await _ofertasLaboralesCollection.InsertOneAsync(document);
                            Console.WriteLine($"Job ID {jobId} details saved.");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"MongoDB error while saving details for Job ID {jobId}: {e.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to fetch details for Job ID {jobId}.");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error during task execution: {e.Message}");
            }
        }


        private TimeSpan CalculateTimeUntilNextRun(string cronExpression)
        {
            // Parse the cron expression to determine the next execution time
            var cronSchedule = CrontabSchedule.Parse(cronExpression);
            DateTime now = DateTime.Now;
            DateTime nextRun = cronSchedule.GetNextOccurrence(now);

            return nextRun - now;
        }
    }

    // Class to deserialize job IDs from a JSON file (not used anymore)
    public class JobIdsContainer
    {
        public List<long> filtered_job_ids { get; set; }
    }
}
