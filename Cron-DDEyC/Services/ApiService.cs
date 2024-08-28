using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cron_BolsaDeTrabajo.Services
{
    // Interface for API Service
    public interface IApiService
    {
        Task<string> CallApiAsync(string url);
    }

    // Implementation of API Service
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> CallApiAsync(string url)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"HTTP Request error: {e.Message}");
                return null;
            }
        }
    }
}
