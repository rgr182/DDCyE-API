using DDEyC_Assistant.Models;

namespace DDEyC_Assistant.Repositories
{
    public interface IChatRepository
    {
        Task<UserThread> GetActiveThreadForUser(int userId);
        Task<UserThread> CreateThreadForUser(int userId, string threadId);
        Task UpdateThreadLastUsed(int userThreadId);
        Task DeactivateThread(int userThreadId);
        Task<List<Message>> GetMessagesForThread(int threadId);
        Task AddMessage(int userThreadId, string content, string role);
        Task<List<UserThread>> GetAllThreadsForUser(int userId);
        Task<UserThread> GetThreadById(int threadId);
        Task<List<UserThread>> GetRecentThreadsForUser(int userId, int count);
        Task DeleteOldMessages(TimeSpan retentionPeriod);
    }
}