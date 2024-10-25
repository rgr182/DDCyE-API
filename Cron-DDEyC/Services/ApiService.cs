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
        Task<List<int>> FetchJobIdsFromDBAsync();
        Task<string> FetchJobDetailsAsync(int jobId);
    }

    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _bearerToken;
        private readonly IConfiguration _configuration;
        private readonly ILinkedInJobService _linkedInJobService; // Nuevo servicio para MongoDB

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

        public async Task<List<int>> FetchJobIdsFromDBAsync()
        {
            try
            {
                // Llama al servicio de MongoDB para obtener los IDs de LinkedIn
                var jobIdsFromMongo = await _linkedInJobService.GetLinkedInJobIdsFromDBAsync();

                // Convertir los IDs obtenidos de MongoDB a una lista de enteros, asumiendo que los IDs en Mongo son strings numéricos
                var jobIds = new List<int>();
                foreach (var id in jobIdsFromMongo)
                {
                    if (int.TryParse(id, out int parsedId))
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
