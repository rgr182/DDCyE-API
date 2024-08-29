using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Cron_BolsaDeTrabajo.Services
{
    public interface IApiService
    {
        Task<List<int>> FetchJobIdsAsync();
        Task<string> FetchJobDetailsAsync(int jobId);
    }

    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _bearerToken;
        private readonly IConfiguration _configuration;

        public ApiService(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _baseUrl = configuration["Api:BaseUrl"];
            _bearerToken = configuration["Api:BearerToken"];
            _configuration = configuration;

            // Set the bearer token for authorization
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        }

        public async Task<List<int>> FetchJobIdsAsync()
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
                    return JsonSerializer.Deserialize<List<int>>(responseBody);
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

            return new List<int>();
        }

        public async Task<string> FetchJobDetailsAsync(int jobId)
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
    }
}
