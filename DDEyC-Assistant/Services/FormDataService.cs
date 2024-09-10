using DDEyC_Assistant.Services.Interfaces;
using DDEyC_Assistant.Models.Entities;
using System.Net.Http;
using System.Text.Json;

namespace DDEyC_Assistant.Services
{
    public class FormDataService : IFormDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FormDataService> _logger;

        public FormDataService(HttpClient httpClient, ILogger<FormDataService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<AgeResponse> ProcessFormDataAsync(FormData formData)
        {
            _logger.LogInformation($"Processing form data: {JsonSerializer.Serialize(formData)}");

            try
            {
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5153/api/AgeResponse/SubmitData", formData);
                
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Received response: {content}");

                var ageResponse = JsonSerializer.Deserialize<AgeResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (ageResponse == null || string.IsNullOrEmpty(ageResponse.Message) || string.IsNullOrEmpty(ageResponse.Category))
                {
                    _logger.LogWarning("Received null or invalid AgeResponse");
                    return new AgeResponse 
                    { 
                        Message = "An error occurred while processing your information.", 
                        Category = "Error" 
                    };
                }

                return ageResponse;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to submit form data");
                return new AgeResponse 
                { 
                    Message = "An error occurred while submitting your information.", 
                    Category = "Error" 
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize AgeResponse");
                return new AgeResponse 
                { 
                    Message = "An error occurred while processing the server response.", 
                    Category = "Error" 
                };
            }
        }
    }
}