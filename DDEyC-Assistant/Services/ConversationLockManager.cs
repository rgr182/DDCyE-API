using System.Collections.Concurrent;
namespace DDEyC_Assistant.Services{
    
public class ConversationLockManager : IConversationLockManager
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _conversationLocks = new();
        
        public async Task<bool> AcquireLock(string conversationId, TimeSpan timeout)
        {
            var conversationLock = _conversationLocks.GetOrAdd(conversationId, _ => new SemaphoreSlim(1, 1));
            return await conversationLock.WaitAsync(timeout);
        }

        public void ReleaseLock(string conversationId)
        {
            if (_conversationLocks.TryGetValue(conversationId, out var conversationLock))
            {
                conversationLock.Release();
            }
        }
    }
}