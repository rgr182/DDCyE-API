using DDEyC_Assistant.Services.Interfaces;
using DDEyC_Assistant.Models.DTOs;
using DDEyC_Assistant.Models.Entities;
using DDEyC_API.Models.DTOs;
using System.Text.Json;
using OpenAI.Assistants;
using Microsoft.AspNetCore.WebUtilities;

namespace DDEyC_Assistant.Services
{
    public class ChatService : IChatService
    {
        private readonly IAssistantService _assistantService;
        private readonly IFormDataService _formDataService;
        private readonly ILogger<ChatService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public ChatService(
            IAssistantService assistantService,
            IFormDataService formDataService,
            ILogger<ChatService> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _assistantService = assistantService;
            _formDataService = formDataService;
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<ChatStartResultDto> StartChatAsync()
        {
            var thread = await _assistantService.CreateThreadAsync();
            const string welcomeMessage = "Hello! I'm a chatbot designed to help you look for jobs. I'm here to help you. What can I do for you?";
            
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
                case "get_job_listings":
                    var args = JsonSerializer.Deserialize<JobListingFilter>(arguments);
                    var queryParams = new Dictionary<string, string>
                    {
                        { nameof(args.Title), args.Title },
                        { nameof(args.CompanyName), args.CompanyName },
                        { nameof(args.Location), args.Location },
                        { nameof(args.Seniority), args.Seniority },
                        { nameof(args.EmploymentType), args.EmploymentType },
                        { nameof(args.Limit), args.Limit.ToString() }
                    };
                    if (args.JobFunctions != null)
                        foreach (var func in args.JobFunctions)
                            queryParams.Add("JobFunctions", func);
                    if (args.Industries != null)
                        foreach (var industry in args.Industries)
                            queryParams.Add("Industries", industry);

                    var url = $"{_configuration["AppSettings:BackEndUrl"]}/api/joblistings{QueryHelpers.AddQueryString(string.Empty, queryParams)}";
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return content;
                    }
                    return JsonSerializer.Serialize(new { error = "Failed to retrieve job listings" });
                default:
                    _logger.LogWarning($"Unknown function: {functionName}");
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
    }
}