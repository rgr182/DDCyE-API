using DDEyC_Assistant.Services.Interfaces;
using DDEyC_Assistant.Models.DTOs;
using DDEyC_Assistant.Models.Entities;
using System.Text.Json;
using OpenAI.Assistants;

namespace DDEyC_Assistant.Services
{
    public class ChatService : IChatService
    {
        private readonly IAssistantService _assistantService;
        private readonly IFormDataService _formDataService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IAssistantService assistantService,
            IFormDataService formDataService,
            ILogger<ChatService> logger)
        {
            _assistantService = assistantService;
            _formDataService = formDataService;
            _logger = logger;
        }

        public async Task<ChatStartResultDto> StartChatAsync()
        {
            var thread = await _assistantService.CreateThreadAsync();
            const string welcomeMessage = "Hello! I'm here to collect some information from you. Let's start with your name. What's your full name?";
            
            await _assistantService.AddMessageToThreadAsync(thread.Id, welcomeMessage, MessageRole.Assistant);
            return new ChatStartResultDto { WelcomeMessage = welcomeMessage, ThreadId = thread.Id };
        }

        public async Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto chatRequest)
        {
            await _assistantService.AddMessageToThreadAsync(chatRequest.ThreadId, chatRequest.UserMessage, MessageRole.User);
            
            var run = await _assistantService.CreateAndRunAssistantAsync(chatRequest.ThreadId);
            
            while (true)
            {
                var retrievedRun = await _assistantService.GetRunAsync(chatRequest.ThreadId, run.Id);

                if (retrievedRun.Status == RunStatus.RequiresAction)
                {
                    var requiredAction = retrievedRun.RequiredActions.First();
                    var functionResult = await HandleFunctionCallAsync(requiredAction.FunctionName, requiredAction.FunctionArguments);
                    await _assistantService.SubmitToolOutputsToRunAsync(chatRequest.ThreadId, retrievedRun.Id, requiredAction.ToolCallId, functionResult);
                }
                else if (retrievedRun.Status == RunStatus.Completed)
                {
                    break;
                }
                else if (retrievedRun.Status == RunStatus.Failed || retrievedRun.Status == RunStatus.Expired)
                {
                    throw new Exception($"Run failed or expired. Status: {retrievedRun.Status}");
                }
                
                await Task.Delay(1000);
            }

            var latestMessage = await _assistantService.GetLatestMessageAsync(chatRequest.ThreadId);
            return new ChatResponseDto { ThreadId = chatRequest.ThreadId, Response = latestMessage?.Content };
        }

         private async Task<string> HandleFunctionCallAsync(string functionName, string arguments)
        {
            _logger.LogInformation($"Handling function call: {functionName} with arguments: {arguments}");
            
            switch (functionName)
            {
                case "submit_form_data":
                    var formData = JsonSerializer.Deserialize<FormData>(arguments);
                    if (formData == null)
                    {
                        _logger.LogError("Failed to deserialize FormData");
                        return JsonSerializer.Serialize(new { error = "Failed to process form data" });
                    }
                    var ageResponse = await _formDataService.ProcessFormDataAsync(formData);
                    _logger.LogInformation($"Age response received: {JsonSerializer.Serialize(ageResponse)}");
                    return JsonSerializer.Serialize(new
                    {
                        message = ageResponse.Message,
                        category = ageResponse.Category
                    });
                default:
                    _logger.LogWarning($"Unknown function: {functionName}");
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
    }
}