using DDEyC_Assistant.Data;
using DDEyC_Assistant.Models;
using Microsoft.EntityFrameworkCore;


namespace DDEyC_Assistant.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly DataContext _context;

        public ChatRepository(DataContext context)
        {
            _context = context;
        }

        public async Task<UserThread> GetActiveThreadForUser(int userId)
        {
            return await _context.UserThreads
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.IsActive);
        }

        public async Task<UserThread> CreateThreadForUser(int userId, string threadId)
        {
            var userThread = new UserThread
            {
                UserId = userId,
                ThreadId = threadId,
                LastUsed = DateTime.UtcNow,
                IsActive = true
            };

            _context.UserThreads.Add(userThread);
            await _context.SaveChangesAsync();

            return userThread;
        }

        public async Task UpdateThreadLastUsed(int userThreadId)
        {
            var userThread = await _context.UserThreads.FindAsync(userThreadId);
            if (userThread != null)
            {
                userThread.LastUsed = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeactivateThread(int userThreadId)
        {
            var userThread = await _context.UserThreads.FindAsync(userThreadId);
            if (userThread != null)
            {
                userThread.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Message>> GetMessagesForThread(int threadId)
        {
            return await _context.Messages
                .Where(m => m.UserThreadId == threadId)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        public async Task AddMessage(int userThreadId, string content, string role)
        {
            var message = new Message
            {
                UserThreadId = userThreadId,
                Content = content,
                Role = role,
                Timestamp = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
        }
        public async Task<List<UserThread>> GetAllThreadsForUser(int userId)
        {
            return await _context.UserThreads
                .Where(ut => ut.UserId == userId)
                .OrderByDescending(ut => ut.LastUsed)
                .ToListAsync();
        }

        public async Task<UserThread> GetThreadById(int threadId)
        {
            return await _context.UserThreads.FindAsync(threadId);
        }



        public async Task<List<UserThread>> GetRecentThreadsForUser(int userId, int count)
        {
            return await _context.UserThreads
                .Where(ut => ut.UserId == userId)
                .OrderByDescending(ut => ut.LastUsed)
                .Take(count)
                .ToListAsync();
        }
    }
}