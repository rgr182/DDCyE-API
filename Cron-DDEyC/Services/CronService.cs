using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Cron_BolsaDeTrabajo.Infrastructure;
using Cron_DDEyC.Utils;
using NCrontab;
using System.Net.Http;

namespace Cron_BolsaDeTrabajo.Services
{
    public interface ICronService
    {
        Task StartAsync();
        Task ExtractJobOffers();
        Task<List<string>> CheckJobUrlsAsync();
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
        private readonly HttpClient _httpClient;

        public CronService(IMongoDbConnection mongoDbConnection, IApiService apiService, IConfiguration configuration, ILinkedInJobService linkedInJobService)
        {
            _mongoDbConnection = mongoDbConnection;
            _apiService = apiService;
            _configuration = configuration;
            _linkedInJobService = linkedInJobService;
            _httpClient = new HttpClient();

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
            _timer = new Timer(async _ => await ExtractJobOffers(), null, timeUntilNextRun, Timeout.InfiniteTimeSpan);
            Console.WriteLine("Cron job initialized.");
        }

        public async Task StartAsync()
        {
            Console.WriteLine("Cron job started.");
            // Further implementation to start the cron job can be added here
        }

        public async Task ExtractJobOffers()
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

        public async Task<List<string>> CheckJobUrlsAsync()
        {
            var errorUrls = new List<string>();

            try
            {
                // Get job URLs from the LinkedInJobService
                //var jobUrls = await _linkedInJobService.GetLinkedInJobUrlsFromDBAsync();

                var jobUrls = new List<string> { "url" };

                // Check each URL with an HTTP GET request
                foreach (var url in jobUrls)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Error fetching URL {url}, status code: {response.StatusCode}");
                            errorUrls.Add(url);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception when fetching URL {url}: {ex.Message}");
                        errorUrls.Add(url);
                    }

                    // Add delay between requests to avoid rate limiting
                    await Task.Delay(750); // Delay of 750 ms
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while checking job URLs: {ex.Message}");
            }

            return errorUrls;
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
}
