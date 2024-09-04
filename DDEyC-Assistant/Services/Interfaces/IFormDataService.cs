using DDEyC_Assistant.Models.Entities;

namespace DDEyC_Assistant.Services.Interfaces
{
    public interface IFormDataService
    {
        Task<AgeResponse> ProcessFormDataAsync(FormData formData);
    }
}