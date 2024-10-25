using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Cron_BolsaDeTrabajo.Services
{
    public interface IApiService
    {
        Task<string> FetchJobDetailsAsync(long jobId);
        Task<List<long>> FetchJobIdsAsync();
        Task<List<long>> FetchJobIdsFromDBAsync();
    }

    public class ApiService : IApiService
    {
        private readonly string _baseUrl;
        private readonly string _bearerToken;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILinkedInJobService _linkedInJobService; 

        public ApiService(IConfiguration configuration, ILinkedInJobService linkedInJobService)
        {
            _httpClient = new HttpClient();
            _baseUrl = configuration["Api:BaseUrl"];
            _bearerToken = configuration["Api:BearerToken"];
            _configuration = configuration;
            _linkedInJobService = linkedInJobService;

            // Set the bearer token for authorization
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        }

        public async Task<string> FetchJobDetailsAsync(long jobId)
        {
            try
            {
                string endpoint = $"{_baseUrl}/collect/{jobId}";

                HttpResponseMessage response = await _httpClient.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    Console.WriteLine($"Error fetching job details for Job ID {jobId}: {response.StatusCode}");
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"HTTP Request error while fetching details for Job ID {jobId}: {e.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error while fetching details for Job ID {jobId}: {e.Message}");
            }

            return null;
        }

        public async Task<List<long>> FetchJobIdsAsync()
        {
            try
            {
                string endpoint = $"{_baseUrl}/search/filter";

                var searchParams = new
                {
                    country = _configuration["SearchParams:Country"],
                    application_active = bool.Parse(_configuration["SearchParams:ApplicationActive"]),
                    deleted = bool.Parse(_configuration["SearchParams:Deleted"]),
                    location = _configuration["SearchParams:Location"]
                };

                string json = JsonSerializer.Serialize(searchParams);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<long>>(responseBody);
                }
                else
                {
                    Console.WriteLine($"Error fetching job IDs: {response.StatusCode}");
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"HTTP Request error while fetching job IDs: {e.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error while fetching job IDs: {e.Message}");
            }

            return new List<long>();
        }

        public async Task<List<long>> FetchJobIdsFromDBAsync()
        {
            try
            {
                // Call the MongoDB service to get LinkedIn job IDs
                var jobIdsFromMongo = await _linkedInJobService.GetLinkedInJobIdsFromDBAsync();

                // Convert the fetched job IDs from MongoDB to a list of long integers, handling numeric BSON types correctly
                var jobIds = new List<long>();
                foreach (var id in jobIdsFromMongo)
                {
                    // Check if the ID is already a valid number or if it can be parsed
                    if (long.TryParse(id, out long parsedId))
                    {
                        jobIds.Add(parsedId);
                    }
                    else
                    {
                        Console.WriteLine($"Invalid ID format: {id}");
                    }
                }

                return jobIds;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"HTTP Request error while fetching job IDs from MongoDB: {e.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error while fetching job IDs from MongoDB: {e.Message}");
            }

            return new List<long>();
        }
    }
}
