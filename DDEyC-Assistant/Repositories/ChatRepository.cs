using DDEyC_Assistant.Data;
using DDEyC_Assistant.Models;
using Microsoft.EntityFrameworkCore;
using OpenAI.Assistants;

namespace DDEyC_Assistant.Repositories
{
   public class ChatRepository : IChatRepository
{
    private readonly DataContext _context;
    private readonly ILogger<ChatRepository> _logger;

    public ChatRepository(DataContext context, ILogger<ChatRepository> logger)
    {
        _context = context;
        _logger = logger;
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

            // Also clear any pending conversation state
            var conversationState = await GetConversationState(userThread.ThreadId);
            if (conversationState != null)
            {
                _context.ConversationStates.Remove(conversationState);
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task<List<Message>> GetMessagesForThread(int userThreadId)
    {
        return await _context.Messages
            .Where(m => m.UserThreadId == userThreadId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<Message> AddMessage(int userThreadId, string content, MessageRole role)
    {
        var message = new Message
        {
            UserThreadId = userThreadId,
            Content = content,
            Role = role.ToString(),
            Timestamp = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task<bool> DeleteMessage(int messageId)
    {
        try
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null)
            {
                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully deleted message {MessageId}", messageId);
                return true;
            }
            
            _logger.LogWarning("Attempted to delete non-existent message {MessageId}", messageId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
            throw;
        }
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
        return await _context.UserThreads
            .FirstOrDefaultAsync(ut => ut.Id == threadId);
    }

    public async Task<List<UserThread>> GetRecentThreadsForUser(int userId, int count)
    {
        return await _context.UserThreads
            .Where(ut => ut.UserId == userId)
            .OrderByDescending(ut => ut.LastUsed)
            .Take(count)
            .ToListAsync();
    }

    public async Task<ConversationStateEntity> GetConversationState(string conversationId)
    {
        return await _context.ConversationStates
            .FirstOrDefaultAsync(s => s.ConversationId == conversationId);
    }

    public async Task UpdateConversationState(string conversationId, ConversationState state, string runId)
    {
        var operationState = await GetConversationState(conversationId);
        if (operationState == null)
        {
            operationState = new ConversationStateEntity
            {
                ConversationId = conversationId
            };
            _context.ConversationStates.Add(operationState);
        }

        operationState.State = state;
        operationState.LastOperation = DateTime.UtcNow;
        operationState.CurrentRunId = runId;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict updating conversation state for {ConversationId}", conversationId);
            // Reload and retry once
            _context.Entry(operationState).Reload();
            operationState.State = state;
            operationState.LastOperation = DateTime.UtcNow;
            operationState.CurrentRunId = runId;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteOldMessages(TimeSpan retentionPeriod)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(retentionPeriod);
            
            // First clean up old conversation states
            var oldStates = await _context.ConversationStates
                .Where(s => s.LastOperation < cutoffDate)
                .ToListAsync();

            if (oldStates.Any())
            {
                _context.ConversationStates.RemoveRange(oldStates);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} old conversation states", oldStates.Count);
            }

            // Then clean up old messages
            var oldMessages = await _context.Messages
                .Where(m => m.Timestamp < cutoffDate)
                .ToListAsync();

            if (oldMessages.Any())
            {
                _context.Messages.RemoveRange(oldMessages);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} old messages", oldMessages.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during old message cleanup");
            throw;
        }
    }
}
}