using DDEyC_Assistant.Models;
using DDEyC_Assistant.Models.DTOs;
using DDEyC_Assistant.Repositories;
using DDEyC_Assistant.Services.Interfaces;
using DDEyC_Assistant.Exceptions;
using System.Text.Json;
using DDEyC_API.Models.DTOs;
using Microsoft.AspNetCore.WebUtilities;
using OpenAI.Assistants;
using DDEyC_Assistant.Models.Entities;

namespace DDEyC_Assistant.Services
{
    public class ChatService : IChatService
    {
        private readonly IAssistantService _assistantService;
        private readonly IChatRepository _chatRepository;
        private readonly ILogger<ChatService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _threadExpirationTime;
        private readonly int _maxRetries;
        private readonly TimeSpan _retryDelay;
        private readonly TimeSpan _runTimeout;

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

            _threadExpirationTime = TimeSpan.FromHours(
                configuration.GetValue("Chat:ThreadExpirationHours", 24));
            _maxRetries = configuration.GetValue("Chat:MaxRetries", 3);
            _retryDelay = TimeSpan.FromSeconds(
                configuration.GetValue("Chat:RetryDelaySeconds", 2));
            _runTimeout = TimeSpan.FromSeconds(
                configuration.GetValue("Chat:RunTimeoutSeconds", 30));
        }

        public async Task<ChatStartResultDto> StartChatAsync(int userId)
        {
            var activeThread = await _chatRepository.GetActiveThreadForUser(userId);

            if (activeThread != null && DateTime.UtcNow - activeThread.LastUsed <= _threadExpirationTime)
            {
                await _chatRepository.UpdateThreadLastUsed(activeThread.Id);
                var messages = await _chatRepository.GetMessagesForThread(activeThread.Id);
                return new ChatStartResultDto
                {
                    ThreadId = activeThread.ThreadId,
                    WelcomeMessage = messages.LastOrDefault()?.Content ?? "Bienvenido de vuelta, continuemos tu anterior conversación.",
                    Messages = messages.Select(m => new MessageDto
                    {
                        Content = m.Content,
                        Role = m.Role,
                        Timestamp = m.Timestamp
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
            await _assistantService.AddMessageToThreadAsync(newThread.Id, welcomeMessage, MessageRole.Assistant);
            await _chatRepository.AddMessage(userThread.Id, welcomeMessage, MessageRole.Assistant);

            return new ChatStartResultDto
            {
                ThreadId = newThread.Id,
                WelcomeMessage = welcomeMessage,
                Messages = new List<MessageDto>
                {
                    new MessageDto
                    {
                        Content = welcomeMessage,
                        Role = MessageRole.Assistant.ToString(),
                        Timestamp = DateTime.UtcNow
                    }
                }
            };
        }

        public async Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto chatRequest, int userId)
        {
            var activeThread = await _chatRepository.GetActiveThreadForUser(userId);
            if (activeThread == null || activeThread.ThreadId != chatRequest.ThreadId)
            {
                throw new ChatServiceException("Invalid thread for this user.", "INVALID_THREAD");
            }

            Message userMessage = null;
            try
            {
                // First try to send message to OpenAI
                await _assistantService.AddMessageToThreadAsync(
                    chatRequest.ThreadId,
                    chatRequest.UserMessage,
                    MessageRole.User);

                // If successful, store the user message
                userMessage = await _chatRepository.AddMessage(
                    activeThread.Id,
                    chatRequest.UserMessage,
                    MessageRole.User);

                await _chatRepository.UpdateThreadLastUsed(activeThread.Id);

                // Create and run the assistant
                var run = await CreateAndRunAssistantWithRetryAsync(chatRequest.ThreadId);
                if (run == null)
                {
                    throw new OpenAIServiceException(
                        "Failed to create and run assistant after multiple retries",
                        "ASSISTANT_CREATE_FAILED");
                }

                // Process the run and get response
                var (success, response) = await ProcessRunWithRetryAsync(chatRequest.ThreadId, run.Id);
                if (!success || response == null)
                {
                    throw new OpenAIServiceException(
                        "Failed to process run after multiple retries",
                        "RUN_PROCESSING_FAILED");
                }

                // Store the assistant's response
                await _chatRepository.AddMessage(
                    activeThread.Id,
                    response.Content,
                    MessageRole.Assistant);

                return new ChatResponseDto
                {
                    ThreadId = chatRequest.ThreadId,
                    Response = response.Content,
                    Status = "success"
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while communicating with OpenAI API");
                await CleanupOnFailure(userMessage?.Id);
                throw OpenAIServiceException.NetworkError(ex);
            }
            catch (Exception ex) when (ex.Message.Contains("rate limit"))
            {
                _logger.LogError(ex, "Rate limit exceeded with OpenAI API");
                await CleanupOnFailure(userMessage?.Id);
                throw OpenAIServiceException.RateLimit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message");
                await CleanupOnFailure(userMessage?.Id);
                throw;
            }
        }

        private async Task CleanupOnFailure(int? messageId)
        {
            if (messageId.HasValue)
            {
                try
                {
                    await _chatRepository.DeleteMessage(messageId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cleanup message {MessageId} after error", messageId);
                }
            }
        }

        private async Task<RunEntity> CreateAndRunAssistantWithRetryAsync(string threadId)
        {
            for (int i = 0; i < _maxRetries; i++)
            {
                try
                {
                    return await _assistantService.CreateAndRunAssistantAsync(threadId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Attempt {Attempt} of {MaxAttempts} to create and run assistant failed",
                        i + 1, _maxRetries);

                    if (i == _maxRetries - 1) throw;
                    await Task.Delay(_retryDelay);
                }
            }
            return null;
        }

        private async Task<(bool success, MessageEntity? response)> ProcessRunWithRetryAsync(string threadId, string runId)
        {
            DateTime timeout = DateTime.UtcNow.Add(_runTimeout);

            while (DateTime.UtcNow < timeout)
            {
                try
                {
                    var retrievedRun = await _assistantService.GetRunAsync(threadId, runId);
                // Process the retrieved run
                // Before you even try to implement this with a switch statement
                // it doesn't work, RunStatus.* are not constants for some reason
                    if (retrievedRun.Status == RunStatus.RequiresAction)
                    {
                        var requiredAction = retrievedRun.RequiredActions.First();
                        var functionResult = await HandleFunctionCallAsync(
                            requiredAction.FunctionName,
                            requiredAction.FunctionArguments);
                        await _assistantService.SubmitToolOutputsToRunAsync(
                            threadId,
                            runId,
                            requiredAction.ToolCallId,
                            functionResult);
                    }
                    else if (retrievedRun.Status == RunStatus.Completed)
                    {
                        var message = await _assistantService.GetLatestMessageAsync(threadId);
                        return (true, message);
                    }
                    else if (retrievedRun.Status == RunStatus.Failed ||
                            retrievedRun.Status == RunStatus.Expired ||
                            retrievedRun.Status == RunStatus.Cancelling ||
                            retrievedRun.Status == RunStatus.Cancelled)
                    {
                        _logger.LogError("Run failed with status: {Status}", retrievedRun.Status);
                        return (false, null);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing run");
                    await Task.Delay(_retryDelay);
                }

                await Task.Delay(1000);
            }

            throw OpenAIServiceException.Timeout();
        }

        private async Task<string> HandleFunctionCallAsync(string functionName, string arguments)
        {
            _logger.LogInformation(
                "Handling function call: {FunctionName} with arguments: {Arguments}",
                functionName,
                arguments);

            try
            {
                switch (functionName)
                {
                    case "get_job_listings":
                        return await HandleGetJobListingsAsync(arguments);
                    default:
                        _logger.LogWarning("Unknown function: {FunctionName}", functionName);
                        return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling function call {FunctionName}", functionName);
                return JsonSerializer.Serialize(new { error = "Internal error processing function call" });
            }
        }

        private async Task<string> HandleGetJobListingsAsync(string arguments)
        {
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

            var queryParams = BuildJobListingQueryParams(args);
            var url = $"{_configuration["AppSettings:BackEndUrl"]}/api/joblistings";

            var response = await _httpClient.GetAsync(QueryHelpers.AddQueryString(url, queryParams));
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            _logger.LogError("Failed to retrieve job listings. Status: {StatusCode}", response.StatusCode);
            return JsonSerializer.Serialize(new { error = "Failed to retrieve job listings" });
        }

        private Dictionary<string, string> BuildJobListingQueryParams(JobListingFilter args)
        {
            var queryParams = new Dictionary<string, string>
            {
                { nameof(JobListingFilter.Title), args.Title },
                { nameof(JobListingFilter.CompanyName), args.CompanyName },
                { nameof(JobListingFilter.Location), args.Location },
                { nameof(JobListingFilter.Seniority), args.Seniority },
                { nameof(JobListingFilter.EmploymentType), args.EmploymentType },
                { nameof(JobListingFilter.Limit), args.Limit.ToString() }
            };

            queryParams = queryParams
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (args.JobFunctions != null)
            {
                foreach (var func in args.JobFunctions)
                {
                    queryParams.Add("JobFunctions", func);
                }
            }

            if (args.Industries != null)
            {
                foreach (var industry in args.Industries)
                {
                    queryParams.Add("Industries", industry);
                }
            }

            return queryParams;
        }

        public async Task<List<MessageDto>> GetMessagesForThread(int userId, int threadId)
        {
            var userThread = await _chatRepository.GetThreadById(threadId);
            if (userThread == null || userThread.UserId != userId)
            {
                throw new ChatServiceException(
                    "Thread not found or does not belong to the user.",
                    "INVALID_THREAD_ACCESS");
            }

            var messages = await _chatRepository.GetMessagesForThread(threadId);
            return messages.Select(m => new MessageDto
            {
                Content = m.Content,
                Role = m.Role,
                Timestamp = m.Timestamp
            }).ToList();
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
    }
}