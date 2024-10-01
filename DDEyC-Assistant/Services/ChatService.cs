using DDEyC_Assistant.Models;
using DDEyC_Assistant.Models.DTOs;
using DDEyC_Assistant.Repositories;
using DDEyC_Assistant.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using DDEyC_API.Models.DTOs;
using Microsoft.AspNetCore.WebUtilities;
using OpenAI.Assistants;

namespace DDEyC_Assistant.Services
{
    public class ChatService : IChatService
    {
        private readonly IAssistantService _assistantService;
        private readonly IChatRepository _chatRepository;
        private readonly ILogger<ChatService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _threadExpirationTime = TimeSpan.FromHours(24);

        public ChatService(
            IAssistantService assistantService,
            IChatRepository chatRepository,
            ILogger<ChatService> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _assistantService = assistantService;
            _chatRepository = chatRepository;
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<ChatStartResultDto> StartChatAsync(int userId)
        {
            var activeThread = await _chatRepository.GetActiveThreadForUser(userId);

            if (activeThread != null && DateTime.UtcNow - activeThread.LastUsed <= _threadExpirationTime)
            {
                await _chatRepository.UpdateThreadLastUsed(activeThread.Id);
                var messages = await _chatRepository.GetMessagesForThread(activeThread.Id);
                var lastMessage = messages.LastOrDefault();
                return new ChatStartResultDto
                {
                    ThreadId = activeThread.ThreadId,
                    WelcomeMessage = lastMessage?.Content ?? "Bienvenido de vuelta, continuemos tu anterior conversación.",
                    Messages = messages.Select(m => new MessageDto
                    {
                        Content = m.Content,
                        Role = m.Role,
                        Timestamp = m.Timestamp  // Use the timestamp from the database
                    }).ToList()
                };
            }

            if (activeThread != null)
            {
                await _chatRepository.DeactivateThread(activeThread.Id);
            }

            var newThread = await _assistantService.CreateThreadAsync();
            var userThread = await _chatRepository.CreateThreadForUser(userId, newThread.Id);

            const string defaultWelcomeMessage = "Hola! soy un chatbot desarrollado para ayudarte a buscar empleo, ¿Qué necesitas?";
            string welcomeMessage = _configuration["Appsettings:WelcomeMessage"] ?? defaultWelcomeMessage;
            await _chatRepository.AddMessage(userThread.Id, welcomeMessage, "assistant");

            return new ChatStartResultDto
            {
                ThreadId = newThread.Id,
                WelcomeMessage = welcomeMessage,
                Messages = new List<MessageDto> { new MessageDto { Content = welcomeMessage, Role = "assistant" } }
            };
        }

        public async Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto chatRequest, int userId)
        {
            var activeThread = await _chatRepository.GetActiveThreadForUser(userId);
            if (activeThread == null || activeThread.ThreadId != chatRequest.ThreadId)
            {
                throw new InvalidOperationException("Invalid thread for this user.");
            }

            await _chatRepository.UpdateThreadLastUsed(activeThread.Id);
            await _chatRepository.AddMessage(activeThread.Id, chatRequest.UserMessage, "user");

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
            if (latestMessage != null)
            {
                await _chatRepository.AddMessage(activeThread.Id, latestMessage.Content, "assistant");
            }

            return new ChatResponseDto { ThreadId = chatRequest.ThreadId, Response = latestMessage?.Content };
        }

        private async Task<string> HandleFunctionCallAsync(string functionName, string arguments)
        {
            _logger.LogInformation($"Handling function call: {functionName} with arguments: {arguments}");

            switch (functionName)
            {
                case "get_job_listings":
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    var args = JsonSerializer.Deserialize<JobListingFilter>(arguments, options);

                    if (args == null)
                    {
                        _logger.LogWarning("Failed to deserialize job listing arguments");
                        return JsonSerializer.Serialize(new { error = "Invalid job listing arguments" });
                    }

                    var queryParams = new Dictionary<string, string>
                    {
                        { nameof(JobListingFilter.Title), args.Title },
                        { nameof(JobListingFilter.CompanyName), args.CompanyName },
                        { nameof(JobListingFilter.Location), args.Location },
                        { nameof(JobListingFilter.Seniority), args.Seniority },
                        { nameof(JobListingFilter.EmploymentType), args.EmploymentType },
                        { nameof(JobListingFilter.Limit), args.Limit.ToString() }
                    };

                    // Remove null values
                    queryParams = queryParams.Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                                             .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    if (args.JobFunctions != null)
                        foreach (var func in args.JobFunctions)
                            queryParams.Add("JobFunctions", func);
                    if (args.Industries != null)
                        foreach (var industry in args.Industries)
                            queryParams.Add("Industries", industry);

                    var url = $"{_configuration["AppSettings:BackEndUrl"]}/api/joblistings";

                    _logger.LogInformation($"Getting job listings with query params: {JsonSerializer.Serialize(queryParams)}");

                    var response = await _httpClient.GetAsync(QueryHelpers.AddQueryString(url, queryParams));
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

        public async Task<List<UserThreadDto>> GetAllThreadsForUser(int userId)
        {
            var threads = await _chatRepository.GetAllThreadsForUser(userId);
            return threads.Select(MapUserThreadToDto).ToList();
        }

        public async Task<UserThreadDto> GetThreadById(int threadId)
        {
            var thread = await _chatRepository.GetThreadById(threadId);
            return thread != null ? MapUserThreadToDto(thread) : null;
        }

        public async Task<List<UserThreadDto>> GetRecentThreadsForUser(int userId, int count)
        {
            var threads = await _chatRepository.GetRecentThreadsForUser(userId, count);
            return threads.Select(MapUserThreadToDto).ToList();
        }

        private UserThreadDto MapUserThreadToDto(UserThread thread)
        {
            return new UserThreadDto
            {
                Id = thread.Id,
                ThreadId = thread.ThreadId,
                LastUsed = thread.LastUsed,
                IsActive = thread.IsActive
            };
        }
        public async Task<List<MessageDto>> GetMessagesForThread(int userId, int threadId)
        {
            var userThread = await _chatRepository.GetThreadById(threadId);
            if (userThread == null || userThread.UserId != userId)
            {
                throw new InvalidOperationException("Thread not found or does not belong to the user.");
            }

            var messages = await _chatRepository.GetMessagesForThread(threadId);
            return messages.Select(m => new MessageDto
            {
                Content = m.Content,
                Role = m.Role,
                Timestamp = m.Timestamp
            }).ToList();
        }
    }
}