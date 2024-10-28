using DDEyC_Assistant.Models.DTOs;

namespace DDEyC_Assistant.Services.Interfaces
{
public interface IChatService
{
    // Chat initialization and processing
    Task<ChatStartResultDto> StartChatAsync(int userId);
    Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto chatRequest, int userId);

    // Thread management
    Task<List<UserThreadDto>> GetAllThreadsForUser(int userId);
    Task<UserThreadDto> GetThreadById(int threadId);
    Task<List<UserThreadDto>> GetRecentThreadsForUser(int userId, int count);
    Task<List<MessageDto>> GetMessagesForThread(int userId, int threadId);

    // Favorites functionality
    Task<bool> ToggleThreadFavoriteAsync(int userId, int threadId, string note);
    Task<bool> ToggleMessageFavoriteAsync(int userId, int messageId, string note);
    Task<List<UserThreadDto>> GetFavoriteThreadsAsync(int userId);
    Task<List<MessageDto>> GetFavoriteMessagesAsync(int userId);
}
}