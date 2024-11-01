using DDEyC_Assistant.Models.Entities;
using OpenAI.Assistants;

namespace DDEyC_Assistant.Services.Interfaces
{
    public interface IAssistantService
    {
        Task<ThreadEntity> CreateThreadAsync();
        Task AddMessageToThreadAsync(string threadId, string content, MessageRole role);
        Task<RunEntity> CreateAndRunAssistantAsync(string threadId);
        Task<RunEntity> GetRunAsync(string threadId, string runId);
        Task SubmitToolOutputsToRunAsync(string threadId, string runId, IEnumerable<ToolOutput> outputs);
        Task<MessageEntity> GetLatestMessageAsync(string threadId);
    }
}