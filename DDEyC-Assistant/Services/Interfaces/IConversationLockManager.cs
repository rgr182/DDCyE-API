namespace DDEyC_Assistant.Services
{
    public interface IConversationLockManager
    {
        Task<bool> AcquireLock(string conversationId, TimeSpan timeout);
        void ReleaseLock(string conversationId);
    }

 
}