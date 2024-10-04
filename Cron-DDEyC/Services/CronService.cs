using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Cron_BolsaDeTrabajo.Infrastructure;
using NCrontab;
using System.Text.Json; 

namespace Cron_BolsaDeTrabajo.Services
{
    public interface ICronService
    {
        Task StartAsync();
        Task TestCoreSignalAsync();

        Task ExecuteTaskAsync();
    }

    public class CronService : ICronService
    {
        private readonly IMongoDbConnection _mongoDbConnection;
        private readonly IApiService _apiService;
        private readonly IMongoCollection<BsonDocument> _ofertasLaboralesCollection;
        private Timer _timer;
        private readonly string _cronExpression;
        private readonly IConfiguration _configuration;

        public CronService(IMongoDbConnection mongoDbConnection, IApiService apiService, IConfiguration configuration)
        {
            _mongoDbConnection = mongoDbConnection;
            _apiService = apiService;
            _configuration = configuration;

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

#if TESTING
            // In testing mode, directly run the test method
            Task.Run(TestCoreSignalAsync).Wait();
#else
            // In normal mode, setup the cron job
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

        public async Task TestCoreSignalAsync()
        {
            Console.WriteLine("Starting API call tests...");

            try
            {
                // Test fetching job IDs
                List<int> jobIds = await _apiService.FetchJobIdsAsync();
                if (jobIds.Count > 0)
                {
                    Console.WriteLine($"Fetched {jobIds.Count} job IDs from API.");
                }
                else
                {
                    Console.WriteLine("No job IDs fetched from API.");
                }

                // Test fetching job details for a limited number of jobs
                int maxCollects = int.Parse(_configuration["Api:MaxCollects"]);

                for (int i = 0; i < Math.Min(jobIds.Count, maxCollects); i++)
                {
                    int jobId = jobIds[i];
                    string jobDetails = await _apiService.FetchJobDetailsAsync(jobId);

                    if (jobDetails != null)
                    {
                        Console.WriteLine($"Job ID {jobId} details fetched successfully, testing saving to MongoDB...");
                        var document = BsonDocument.Parse(jobDetails);

                        try
                        {
                            await _ofertasLaboralesCollection.InsertOneAsync(document);
                            Console.WriteLine($"Job ID {jobId} details saved to MongoDB.");
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

                Console.WriteLine("API call tests completed.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error during API call tests: {e.Message}");
            }
        }

        public async Task ExecuteTaskAsync()
        {
            Console.WriteLine($"Executing task at {DateTime.Now}");

            try
            {
                // Cargar IDs desde el archivo JSON local usando System.Text.Json
                string jsonFilePath = Path.Combine(AppContext.BaseDirectory, "JsonFiles", "JsonRaul.json");
                if (File.Exists(jsonFilePath))
                {
                    var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                    var jobData = JsonSerializer.Deserialize<JobIdsContainer>(jsonContent);
                    List<int> jobIds = jobData?.filtered_job_ids ?? new List<int>();

                    int maxCollects = int.Parse(_configuration["Api:MaxCollects"]);

                    for (int i = 0; i < Math.Min(jobIds.Count, maxCollects); i++)
                    {
                        int jobId = jobIds[i];
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
                    }
                }
                else
                {
                    Console.WriteLine($"Error: JSON file {jsonFilePath} not found.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error during task execution: {e.Message}");
            }
        }

        private TimeSpan CalculateTimeUntilNextRun(string cronExpression)
        {
            var cronSchedule = CrontabSchedule.Parse(cronExpression);
            DateTime now = DateTime.Now;
            DateTime nextRun = cronSchedule.GetNextOccurrence(now);

            return nextRun - now;
        }
    }

    // Clase para deserializar el archivo JSON
    public class JobIdsContainer
    {
        public List<int> filtered_job_ids { get; set; }
    }
}
