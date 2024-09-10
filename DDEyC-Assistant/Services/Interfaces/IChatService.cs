using DDEyC_Assistant.Models.DTOs;

namespace DDEyC_Assistant.Services.Interfaces
{
    public interface IChatService
    {
        Task<ChatStartResultDto> StartChatAsync();
        Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto chatRequest);
    }
}