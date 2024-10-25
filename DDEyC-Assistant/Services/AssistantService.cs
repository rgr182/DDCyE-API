using DDEyC_Assistant.Services.Interfaces;
using DDEyC_Assistant.Models.Entities;

using OpenAI.Assistants;
using OpenAI;

namespace DDEyC_Assistant.Services
{
    public class AssistantService : IAssistantService
    {
        private readonly OpenAIClient _openAiClient;
        private readonly string _assistantId;
        private readonly ILogger<AssistantService> _logger;

        public AssistantService(IConfiguration configuration, ILogger<AssistantService> logger)
        {
            string apiKey = Environment.GetEnvironmentVariable("MY_OPEN_AI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not set in environment variables.");
            }
            _openAiClient = new OpenAIClient(apiKey);
            _assistantId = configuration["OpenAI-Identifiers:FormAssistant"];
            _logger = logger;
        }

        // Rest of the methods remain the same
        
        public async Task<ThreadEntity> CreateThreadAsync()
        {
            #pragma warning disable OPENAI001
            var thread = await _openAiClient.GetAssistantClient().CreateThreadAsync();
            #pragma warning restore OPENAI001
            return new ThreadEntity { Id = thread.Value.Id };
        }

        public async Task AddMessageToThreadAsync(string threadId, string content, MessageRole role)
        {
            #pragma warning disable OPENAI001
            var thread = _openAiClient.GetAssistantClient().GetThread(threadId);
            await _openAiClient.GetAssistantClient().CreateMessageAsync(thread, role, [content]);
            #pragma warning restore OPENAI001
        }

        public async Task<RunEntity> CreateAndRunAssistantAsync(string threadId)
        {
            #pragma warning disable OPENAI001
            var run = await _openAiClient.GetAssistantClient().CreateRunAsync(threadId, _assistantId);
            #pragma warning restore OPENAI001
            return new RunEntity { Id = run.Value.Id, Status = run.Value.Status };
        }

        public async Task<RunEntity> GetRunAsync(string threadId, string runId)
        {
            #pragma warning disable OPENAI001
            var run = await _openAiClient.GetAssistantClient().GetRunAsync(threadId, runId);
            #pragma warning restore OPENAI001
            return new RunEntity 
            { 
                Id = run.Value.Id, 
                Status = run.Value.Status,
                RequiredActions = run.Value.RequiredActions?.Select(a => new RequiredActionEntity
                {
                    ToolCallId = a.ToolCallId,
                    FunctionName = a.FunctionName,
                    FunctionArguments = a.FunctionArguments
                }).ToList()
            };
        }

        public async Task SubmitToolOutputsToRunAsync(string threadId, string runId, string toolCallId, string output)
        {
            #pragma warning disable OPENAI001
            _logger.LogInformation("Submitting tool outputs to run: {ThreadID}, {RunID}, {ToolCallID}, {Output}", threadId, runId, toolCallId, output);
            await _openAiClient.GetAssistantClient().SubmitToolOutputsToRunAsync(
                threadId,
                runId,
                [new ToolOutput(toolCallId, output)]
            );
            #pragma warning restore OPENAI001
        }

        public  async Task<MessageEntity> GetLatestMessageAsync(string threadId)
        {
            #pragma warning disable OPENAI001
            var messages = _openAiClient.GetAssistantClient().GetMessagesAsync(threadId);
            var latestMessage = messages.GetAllValuesAsync().ToBlockingEnumerable().FirstOrDefault();;
            #pragma warning restore OPENAI001

            return latestMessage != null
                ? new MessageEntity { Content = latestMessage.Content.FirstOrDefault()?.Text }
                : null;
        }
    }
}