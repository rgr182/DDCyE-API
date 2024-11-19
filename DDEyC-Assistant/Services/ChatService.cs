using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using DDEyC_API.Models.DTOs;
using DDEyC_Assistant.Exceptions;
using DDEyC_Assistant.Models;
using DDEyC_Assistant.Models.DTOs;
using DDEyC_Assistant.Models.Entities;
using DDEyC_Assistant.Repositories;
using DDEyC_Assistant.Services;
using DDEyC_Assistant.Services.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using OpenAI.Assistants;

public class ChatService : IChatService
{
    private readonly IAssistantService _assistantService;
    private readonly IChatRepository _chatRepository;
    private readonly ILogger<ChatService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IConversationLockManager _lockManager;
    private readonly TimeSpan _conversationTimeout;
    private readonly TimeSpan _threadExpirationTime;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;
    private readonly TimeSpan _runTimeout;

    public ChatService(
        IAssistantService assistantService,
        IChatRepository chatRepository,
        ILogger<ChatService> logger,
        HttpClient httpClient,
        IConfiguration configuration,
        IConversationLockManager lockManager)
    {
        _assistantService = assistantService;
        _chatRepository = chatRepository;
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
        _lockManager = lockManager;

        _conversationTimeout = TimeSpan.FromMinutes(5); // Can be moved to configuration
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
                Id = activeThread.Id,
                ThreadId = activeThread.ThreadId,
                WelcomeMessage = messages.LastOrDefault()?.Content ?? "Bienvenido de vuelta, continuemos tu anterior conversación.",
                Messages = messages.Select(m => new MessageDto
                {
                    Id = m.Id,
                    Content = m.Content,
                    IsFavorite = m.IsFavorite,
                    FavoriteNote = m.FavoriteNote,
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
        var addedMessage = await _chatRepository.AddMessage(userThread.Id, welcomeMessage, MessageRole.Assistant);

        return new ChatStartResultDto
        {
            Id = userThread.Id,
            ThreadId = newThread.Id,
            WelcomeMessage = welcomeMessage,
            Messages = new List<MessageDto>
            {
                new MessageDto
                {
                    Id = addedMessage.Id,
                    Content = welcomeMessage,
                    IsFavorite = false,
                    FavoriteNote = string.Empty,
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
        var lockAcquired = false;

        try
        {
            // Try to acquire lock for this specific conversation
            lockAcquired = await _lockManager.AcquireLock(chatRequest.ThreadId, TimeSpan.FromSeconds(2));
            if (!lockAcquired)
            {
                throw new ChatServiceException(
                    "Esta conversación está procesando otro mensaje. Por favor, espere.",
                    "CONVERSATION_BUSY");
            }

            // Check if there's a pending operation
            var conversationState = await _chatRepository.GetConversationState(chatRequest.ThreadId);
            if (conversationState?.State == ConversationState.Processing)
            {
                // Check for stale operations
                if (DateTime.UtcNow - conversationState.LastOperation > _conversationTimeout)
                {
                    await _chatRepository.UpdateConversationState(
                        chatRequest.ThreadId,
                        ConversationState.Error,
                        null);
                }
                else
                {
                    throw new ChatServiceException(
                        "Esta conversación está procesando otro mensaje. Por favor, espere.",
                        "PROCESSING_IN_PROGRESS");
                }
            }

            // Mark conversation as processing
            await _chatRepository.UpdateConversationState(
                chatRequest.ThreadId,
                ConversationState.Processing,
                null);

            try
            {
                await _assistantService.AddMessageToThreadAsync(
                    chatRequest.ThreadId,
                    chatRequest.UserMessage,
                    MessageRole.User);

                userMessage = await _chatRepository.AddMessage(
                    activeThread.Id,
                    chatRequest.UserMessage,
                    MessageRole.User);

                await _chatRepository.UpdateThreadLastUsed(activeThread.Id);

                var run = await CreateAndRunAssistantWithRetryAsync(chatRequest.ThreadId);
                if (run == null)
                {
                    throw new OpenAIServiceException(
                        "Failed to create assistant response",
                        "ASSISTANT_CREATE_FAILED");
                }

                await _chatRepository.UpdateConversationState(
                    chatRequest.ThreadId,
                    ConversationState.Processing,
                    run.Id);

                var (success, response) = await ProcessRunWithRetryAsync(chatRequest.ThreadId, run.Id);
                if (!success || response == null)
                {
                    throw new OpenAIServiceException(
                        "Failed to process assistant response",
                        "RUN_PROCESSING_FAILED");
                }

                await _chatRepository.AddMessage(
                    activeThread.Id,
                    response.Content,
                    MessageRole.Assistant);

                await _chatRepository.UpdateConversationState(
                    chatRequest.ThreadId,
                    ConversationState.Idle,
                    null);

                return new ChatResponseDto
                {
                    MessageId = userMessage.Id,
                    ThreadId = chatRequest.ThreadId,
                    Response = response.Content,
                    Status = "success"
                };
            }
            catch (Exception ex) when (ex is TimeoutException || ex is TaskCanceledException ||
                                     (ex is OpenAIServiceException oaiEx && oaiEx.ErrorCode == "TIMEOUT"))
            {
                _logger.LogError(ex, "Timeout occurred while processing chat message");
                await CleanupOnFailure(userMessage?.Id);
                throw new OpenAIServiceException("La operación ha excedido el tiempo de espera.", "TIMEOUT");
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
            finally
            {
                // Always ensure we update the conversation state on any failure
                try
                {
                    await _chatRepository.UpdateConversationState(
                        chatRequest.ThreadId,
                        ConversationState.Idle,
                        null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating conversation state during cleanup");
                }
            }
        }
        finally
        {
            // Always release the lock if we acquired it
            if (lockAcquired)
            {
                _lockManager.ReleaseLock(chatRequest.ThreadId);
            }
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

    // In ChatService.cs, update ProcessRunWithRetryAsync
    private async Task<(bool success, MessageEntity? response)> ProcessRunWithRetryAsync(string threadId, string runId)
    {
        DateTime timeout = DateTime.UtcNow.Add(_runTimeout);

        while (DateTime.UtcNow < timeout)
        {
            try
            {
                var retrievedRun = await _assistantService.GetRunAsync(threadId, runId);

                if (retrievedRun.Status == RunStatus.RequiresAction)
                {
                    // Process all tools and collect their outputs
                    var toolOutputs = await Task.WhenAll(
                        retrievedRun.RequiredActions.Select(async action =>
                        {
                            var result = await HandleFunctionCallAsync(
                                action.FunctionName,
                                action.FunctionArguments);
                            return new ToolOutput(action.ToolCallId, result);
                        })
                    );

                    // Submit all outputs in a single API call
                    await _assistantService.SubmitToolOutputsToRunAsync(
                        threadId,
                        runId,
                        toolOutputs);

                    await Task.Delay(1000);
                    continue;
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
                case "get_course_recommendations":
                    return await HandleGetCourseRecommendationsAsync(arguments);
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

        try
        {
            var queryParams = BuildJobListingQueryParams(args);
            var url = $"{_configuration["AppSettings:BackEndUrl"]}/api/joblistings";

            // QueryHelpers.AddQueryString accepts IEnumerable<KeyValuePair<string, string>> directly
            var response = await _httpClient.GetAsync(QueryHelpers.AddQueryString(url, queryParams));
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            _logger.LogError("Failed to retrieve job listings. Status: {StatusCode}", response.StatusCode);
            return JsonSerializer.Serialize(new { error = "Failed to retrieve job listings" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building or executing job listings query");
            return JsonSerializer.Serialize(new { error = "Failed to process job listings request" });
        }
    }
    private async Task<string> HandleGetCourseRecommendationsAsync(string arguments)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var args = JsonSerializer.Deserialize<CourseFilter>(arguments, options);
        if (args == null)
        {
            _logger.LogWarning("Failed to deserialize course recommendation arguments");
            return JsonSerializer.Serialize(new { error = "Invalid course recommendation arguments" });
        }

        var url = $"{_configuration["AppSettings:BackEndUrl"]}/api/Courses/recommendations";
        var content = new StringContent(
            JsonSerializer.Serialize(args, options),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        var response = await _httpClient.PostAsync(url, content);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }

        _logger.LogError("Failed to retrieve course recommendations. Status: {StatusCode}", response.StatusCode);
        return JsonSerializer.Serialize(new { error = "Failed to retrieve course recommendations" });
    }
    // Change return type to match what QueryHelpers.AddQueryString actually needs
    private IEnumerable<KeyValuePair<string, string>> BuildJobListingQueryParams(JobListingFilter args)
    {
        var queryParams = new List<KeyValuePair<string, string>>();

        // Single-value parameters
        if (!string.IsNullOrEmpty(args.Title))
            queryParams.Add(new KeyValuePair<string, string>("Title", args.Title));
        if (!string.IsNullOrEmpty(args.CompanyName))
            queryParams.Add(new KeyValuePair<string, string>("CompanyName", args.CompanyName));
        if (!string.IsNullOrEmpty(args.Location))
            queryParams.Add(new KeyValuePair<string, string>("Location", args.Location));
        if (!string.IsNullOrEmpty(args.Seniority))
            queryParams.Add(new KeyValuePair<string, string>("Seniority", args.Seniority));
        if (!string.IsNullOrEmpty(args.EmploymentType))
            queryParams.Add(new KeyValuePair<string, string>("EmploymentType", args.EmploymentType));

        queryParams.Add(new KeyValuePair<string, string>("Limit", args.Limit.ToString()));

        // Multi-value parameters
        if (args.JobFunctions?.Any() == true)
        {
            foreach (var function in args.JobFunctions.Where(f => !string.IsNullOrEmpty(f)))
            {
                queryParams.Add(new KeyValuePair<string, string>("JobFunctions", function));
            }
        }

        if (args.Industries?.Any() == true)
        {
            foreach (var industry in args.Industries.Where(i => !string.IsNullOrEmpty(i)))
            {
                queryParams.Add(new KeyValuePair<string, string>("Industries", industry));
            }
        }

        if (args.AcademicLevels?.Any() == true)
        {
            foreach (var level in args.AcademicLevels.OrderBy(x => x))
            {
                queryParams.Add(new KeyValuePair<string, string>("AcademicLevels", level.ToString()));
            }
        }

        if (args.MinimumAcademicLevel != 0)
            queryParams.Add(new KeyValuePair<string, string>("MinimumAcademicLevel", args.MinimumAcademicLevel.ToString()));

        return queryParams.Where(kvp => !string.IsNullOrEmpty(kvp.Value));
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
            Id = m.Id,
            Content = m.Content,
            Role = m.Role,
            IsFavorite = m.IsFavorite,
            FavoriteNote = m.FavoriteNote,
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
    public async Task<bool> ToggleThreadFavoriteAsync(int userId, int threadId, string note)
    {
        try
        {
            return await _chatRepository.ToggleThreadFavorite(userId, threadId, note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling thread favorite for user {UserId}, thread {ThreadId}", userId, threadId);
            throw new ChatServiceException("Failed to toggle thread favorite", "FAVORITE_TOGGLE_FAILED");
        }
    }

    public async Task<bool> ToggleMessageFavoriteAsync(int userId, int messageId, string note)
    {
        try
        {
            return await _chatRepository.ToggleMessageFavorite(userId, messageId, note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling message favorite for user {UserId}, message {MessageId}", userId, messageId);
            throw new ChatServiceException("Failed to toggle message favorite", "FAVORITE_TOGGLE_FAILED");
        }
    }

    public async Task<List<UserThreadDto>> GetFavoriteThreadsAsync(int userId)
    {
        try
        {
            var favorites = await _chatRepository.GetFavoriteThreads(userId);
            return favorites.Select(MapUserThreadToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorite threads for user {UserId}", userId);
            throw new ChatServiceException("Failed to get favorite threads", "FAVORITE_FETCH_FAILED");
        }
    }

    public async Task<List<MessageDto>> GetFavoriteMessagesAsync(int userId)
    {
        try
        {
            var favorites = await _chatRepository.GetFavoriteMessages(userId);
            return favorites.Select(m => new MessageDto
            {
                Id = m.Id,
                Content = m.Content,
                Role = m.Role,
                Timestamp = m.Timestamp,
                IsFavorite = m.IsFavorite,
                FavoriteNote = m.FavoriteNote
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorite messages for user {UserId}", userId);
            throw new ChatServiceException("Failed to get favorite messages", "FAVORITE_FETCH_FAILED");
        }
    }
    private UserThreadDto MapUserThreadToDto(UserThread thread)
    {
        return new UserThreadDto
        {
            Id = thread.Id,
            ThreadId = thread.ThreadId,
            LastUsed = thread.LastUsed,
            IsFavorite = thread.IsFavorite,
            FavoriteNote = thread.FavoriteNote,
            IsActive = thread.IsActive
        };
    }
}