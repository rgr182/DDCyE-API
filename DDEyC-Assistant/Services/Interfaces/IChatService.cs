using DDEyC_Assistant.Models.DTOs;

namespace DDEyC_Assistant.Services.Interfaces
{
      public interface IChatService
    {
        Task<ChatStartResultDto> StartChatAsync(int userId);
        Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto chatRequest, int userId);
        Task<List<UserThreadDto>> GetAllThreadsForUser(int userId);
        Task<UserThreadDto> GetThreadById(int threadId);
        Task<List<UserThreadDto>> GetRecentThreadsForUser(int userId, int count);
         Task<List<MessageDto>> GetMessagesForThread(int userId, int threadId);

    }
}