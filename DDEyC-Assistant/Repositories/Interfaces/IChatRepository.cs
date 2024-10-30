using DDEyC_Assistant.Models;
using OpenAI.Assistants;

namespace DDEyC_Assistant.Repositories
{
    public interface IChatRepository
    {
        Task<UserThread> GetActiveThreadForUser(int userId);
        Task<UserThread> CreateThreadForUser(int userId, string threadId);
        Task UpdateThreadLastUsed(int userThreadId);
        Task DeactivateThread(int userThreadId);
        Task<List<Message>> GetMessagesForThread(int threadId);
        Task<Message> AddMessage(int userThreadId, string content, MessageRole role);
        Task<List<UserThread>> GetAllThreadsForUser(int userId);
        Task<UserThread> GetThreadById(int threadId);
        Task<List<UserThread>> GetRecentThreadsForUser(int userId, int count);
        Task<bool> DeleteMessage(int messageId);

        Task DeleteOldMessages(TimeSpan retentionPeriod);
        Task<ConversationStateEntity> GetConversationState(string conversationId);
        Task UpdateConversationState(string conversationId, ConversationState state, string runId);
        Task<bool> ToggleThreadFavorite(int userId, int threadId, string note);
        Task<bool> ToggleMessageFavorite(int userId, int messageId, string note);
        Task<List<UserThread>> GetFavoriteThreads(int userId);
        Task<List<Message>> GetFavoriteMessages(int userId);
    }
}