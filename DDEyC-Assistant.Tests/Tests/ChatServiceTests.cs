// Services/ChatServiceTests.cs
using DDEyC_Assistant.Models;
using DDEyC_Assistant.Models.DTOs;
using DDEyC_Assistant.Services.Interfaces;
using DDEyC_Assistant.Exceptions;
using DDEyC_Assistant.Repositories;
using DDEyC_Assistant.Tests.TestHelpers;
using DDEyC_Assistant.Tests.TestData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Assistants;
using Xunit;
using DDEyC_Assistant.Models.Entities;
using DDEyC_Assistant.Services;

namespace DDEyC_Assistant.Tests.Services
{
    public class ChatServiceTests
    {
        private readonly Mock<IAssistantService> _mockAssistantService;
        private readonly Mock<IChatRepository> _mockChatRepository;
        private readonly Mock<ILogger<ChatService>> _mockLogger;
        private readonly Mock<HttpClient> _mockHttpClient;
        private readonly IConfiguration _configuration;
        private readonly ChatService _chatService;
        private readonly Mock<IConversationLockManager> _mockConversationLockManager;


        public ChatServiceTests()
        {
            _mockAssistantService = new Mock<IAssistantService>();
            _mockChatRepository = new Mock<IChatRepository>();
            _mockLogger = new Mock<ILogger<ChatService>>();
            _mockHttpClient = new Mock<HttpClient>();
            _mockConversationLockManager = new Mock<IConversationLockManager>();

            var configurationBuilder = new ConfigurationBuilder();
            var myConfiguration = new Dictionary<string, string>
            {
                {"Chat:ThreadExpirationHours", "24"},
                {"Chat:MaxRetries", "3"},
                {"Chat:RetryDelaySeconds", "2"},
                {"Chat:RunTimeoutSeconds", "30"},
                {"AppSettings:BackEndUrl", "http://localhost"},
                {"AppSettings:WelcomeMessage", "Hello!"}
            };

            configurationBuilder.AddInMemoryCollection(myConfiguration);
            _configuration = configurationBuilder.Build();

            _chatService = new ChatService(
                _mockAssistantService.Object,
                _mockChatRepository.Object,
                _mockLogger.Object,
                _mockHttpClient.Object,
                _configuration,
                _mockConversationLockManager.Object

            );
        }

        [Fact]
        public async Task ProcessChatAsync_OpenAIRateLimit_DeletesMessageAndThrowsException()
        {
            // Arrange
            var userThread = TestDataFactory.CreateUserThread();
            var message = TestDataFactory.CreateMessage(userThreadId: userThread.Id);

            _mockChatRepository.Setup(r => r.GetActiveThreadForUser(userThread.UserId))
                .ReturnsAsync(userThread);

            _mockChatRepository.Setup(r => r.AddMessage(
                userThread.Id,
                message.Content,
                MessageRole.User))
                .ReturnsAsync(message);

            // Setup AddMessageToThreadAsync to succeed
            _mockAssistantService.Setup(s => s.AddMessageToThreadAsync(
                userThread.ThreadId,
                message.Content,
                MessageRole.User))
                .Returns(Task.CompletedTask);

            // Setup CreateAndRunAssistantAsync to throw the rate limit error
            _mockAssistantService.Setup(s => s.CreateAndRunAssistantAsync(userThread.ThreadId))
                .ThrowsAsync(TestExceptions.CreateRateLimitError());

            // Act & Assert
            var exception = await Assert.ThrowsAsync<OpenAIServiceException>(async () =>
                await _chatService.ProcessChatAsync(new ChatRequestDto
                {
                    ThreadId = userThread.ThreadId,
                    UserMessage = message.Content
                }, userThread.UserId));

            Assert.Equal("RATE_LIMIT", exception.ErrorCode);
            _mockChatRepository.Verify(r => r.DeleteMessage(message.Id), Times.Once);
        }

        [Fact]
        public async Task ProcessChatAsync_NetworkError_DeletesMessageAndThrowsException()
        {
            // Arrange
            var userThread = TestDataFactory.CreateUserThread();
            var message = TestDataFactory.CreateMessage(userThreadId: userThread.Id);

            _mockChatRepository.Setup(r => r.GetActiveThreadForUser(userThread.UserId))
                .ReturnsAsync(userThread);

            _mockChatRepository.Setup(r => r.AddMessage(
                userThread.Id,
                message.Content,
                MessageRole.User))
                .ReturnsAsync(message);

            // Setup AddMessageToThreadAsync to succeed
            _mockAssistantService.Setup(s => s.AddMessageToThreadAsync(
                userThread.ThreadId,
                message.Content,
                MessageRole.User))
                .Returns(Task.CompletedTask);

            // Setup CreateAndRunAssistantAsync to throw the network error
            _mockAssistantService.Setup(s => s.CreateAndRunAssistantAsync(userThread.ThreadId))
                .ThrowsAsync(TestExceptions.CreateNetworkError());

            // Act & Assert
            var exception = await Assert.ThrowsAsync<OpenAIServiceException>(async () =>
                await _chatService.ProcessChatAsync(new ChatRequestDto
                {
                    ThreadId = userThread.ThreadId,
                    UserMessage = message.Content
                }, userThread.UserId));

            Assert.Equal("NETWORK_ERROR", exception.ErrorCode);
            _mockChatRepository.Verify(r => r.DeleteMessage(message.Id), Times.Once);
        }

        [Fact]
        public async Task ProcessChatAsync_RunTimeout_DeletesMessageAndThrowsException()
        {
            // Arrange
            var userThread = TestDataFactory.CreateUserThread();
            var message = TestDataFactory.CreateMessage(userThreadId: userThread.Id);
            var run = TestDataFactory.CreateRunInProgress();

            _mockChatRepository.Setup(r => r.GetActiveThreadForUser(userThread.UserId))
                .ReturnsAsync(userThread);

            _mockChatRepository.Setup(r => r.AddMessage(
                userThread.Id,
                message.Content,
                MessageRole.User))
                .ReturnsAsync(message);

            _mockAssistantService.Setup(s => s.CreateAndRunAssistantAsync(userThread.ThreadId))
                .ReturnsAsync(run);

            _mockAssistantService.Setup(s => s.GetRunAsync(userThread.ThreadId, run.Id))
                .ReturnsAsync(run); // Always returns in progress

            // Act & Assert
            var exception = await Assert.ThrowsAsync<OpenAIServiceException>(async () =>
                await _chatService.ProcessChatAsync(new ChatRequestDto
                {
                    ThreadId = userThread.ThreadId,
                    UserMessage = message.Content
                }, userThread.UserId));

            Assert.Equal("TIMEOUT", exception.ErrorCode);
            _mockChatRepository.Verify(r => r.DeleteMessage(message.Id), Times.Once);
        }

        [Fact]
        public async Task ProcessChatAsync_RunFails_DeletesMessageAndThrowsException()
        {
            // Arrange
            var userThread = TestDataFactory.CreateUserThread();
            var message = TestDataFactory.CreateMessage(userThreadId: userThread.Id);
            var initialRun = TestDataFactory.CreateRunInProgress();
            var failedRun = TestDataFactory.CreateRunFailed();

            _mockChatRepository.Setup(r => r.GetActiveThreadForUser(userThread.UserId))
                .ReturnsAsync(userThread);

            _mockChatRepository.Setup(r => r.AddMessage(
                userThread.Id,
                message.Content,
                MessageRole.User))
                .ReturnsAsync(message);

            _mockAssistantService.Setup(s => s.CreateAndRunAssistantAsync(userThread.ThreadId))
                .ReturnsAsync(initialRun);

            var callCount = 0;
            _mockAssistantService.Setup(s => s.GetRunAsync(userThread.ThreadId, initialRun.Id))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return callCount > 1 ? failedRun : initialRun;
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<OpenAIServiceException>(async () =>
                await _chatService.ProcessChatAsync(new ChatRequestDto
                {
                    ThreadId = userThread.ThreadId,
                    UserMessage = message.Content
                }, userThread.UserId));

            Assert.Equal("RUN_PROCESSING_FAILED", exception.ErrorCode);
            _mockChatRepository.Verify(r => r.DeleteMessage(message.Id), Times.Once);
        }

        [Fact]
        public async Task ProcessChatAsync_FunctionCallSuccess_CompletesSuccessfully()
        {
            // Arrange
            var userThread = TestDataFactory.CreateUserThread();
            var message = TestDataFactory.CreateMessage(userThreadId: userThread.Id);
            var initialRun = TestDataFactory.CreateRunInProgress();
            var actionRun = TestDataFactory.CreateRunRequiringAction();
            var completedRun = TestDataFactory.CreateRunCompleted();
            var assistantResponse = TestDataFactory.CreateAssistantMessageEntity("Here are the job listings");

            _mockChatRepository.Setup(r => r.GetActiveThreadForUser(userThread.UserId))
                .ReturnsAsync(userThread);

            _mockChatRepository.Setup(r => r.AddMessage(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<MessageRole>()))
                .ReturnsAsync(message);

            _mockAssistantService.Setup(s => s.CreateAndRunAssistantAsync(userThread.ThreadId))
                .ReturnsAsync(initialRun);
            var callCount = 0;
            _mockAssistantService.Setup(s => s.GetRunAsync(userThread.ThreadId, initialRun.Id))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1) return actionRun;
                    return completedRun;
                });

            _mockAssistantService.Setup(s => s.GetLatestMessageAsync(userThread.ThreadId))
                .ReturnsAsync(assistantResponse);

            // Act
            var result = await _chatService.ProcessChatAsync(new ChatRequestDto
            {
                ThreadId = userThread.ThreadId,
                UserMessage = message.Content
            }, userThread.UserId);

            // Assert
            Assert.Equal("success", result.Status);
            Assert.Equal(assistantResponse.Content, result.Response);
            Assert.Equal(userThread.ThreadId, result.ThreadId);

            _mockAssistantService.Verify(s => s.SubmitToolOutputsToRunAsync(
                userThread.ThreadId,
                initialRun.Id,
                actionRun.RequiredActions.First().ToolCallId,
                It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessChatAsync_InvalidThread_ThrowsException()
        {
            // Arrange
            var userId = 1;
            var threadId = "invalid_thread";

            _mockChatRepository.Setup(r => r.GetActiveThreadForUser(userId))
                .ReturnsAsync((UserThread)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ChatServiceException>(async () =>
                await _chatService.ProcessChatAsync(new ChatRequestDto
                {
                    ThreadId = threadId,
                    UserMessage = "test"
                }, userId));

            Assert.Equal("INVALID_THREAD", exception.ErrorCode);

            // Verify no messages were added
            _mockChatRepository.Verify(r => r.AddMessage(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<MessageRole>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessChatAsync_StartNewChat_CreatesNewThread()
        {
            // Arrange
            var userId = 1;
            var newThreadId = "new_thread";
            var welcomeMessage = "Hello!";

            var newThread = TestDataFactory.CreateUserThread(threadId: newThreadId);
            var message = TestDataFactory.CreateMessage(
                userThreadId: newThread.Id,
                content: welcomeMessage,
                role: MessageRole.Assistant);

            _mockChatRepository.Setup(r => r.GetActiveThreadForUser(userId))
                .ReturnsAsync((UserThread)null);

            _mockAssistantService.Setup(s => s.CreateThreadAsync())
                .ReturnsAsync(new ThreadEntity { Id = newThreadId });

            _mockChatRepository.Setup(r => r.CreateThreadForUser(userId, newThreadId))
                .ReturnsAsync(newThread);

            _mockChatRepository.Setup(r => r.AddMessage(
                newThread.Id,
                welcomeMessage,
                MessageRole.Assistant))
                .ReturnsAsync(message);

            // Act
            var result = await _chatService.StartChatAsync(userId);

            // Assert
            Assert.Equal(newThreadId, result.ThreadId);
            Assert.Equal(welcomeMessage, result.WelcomeMessage);
            Assert.Single(result.Messages);
            Assert.Equal(welcomeMessage, result.Messages[0].Content);
            Assert.Equal(MessageRole.Assistant.ToString(), result.Messages[0].Role);
        }

        [Fact]
        public async Task ProcessChatAsync_ResumeExistingThread_ReturnsExistingMessages()
        {
            // Arrange
            var userId = 1;
            var threadId = "existing_thread";
            var existingThread = TestDataFactory.CreateUserThread(threadId: threadId);
            var existingMessages = new List<Message>
            {
                TestDataFactory.CreateMessage(
                    userThreadId: existingThread.Id,
                    content: "Hello",
                    role: MessageRole.User),
                TestDataFactory.CreateMessage(
                    userThreadId: existingThread.Id,
                    content: "Hi there!",
                    role: MessageRole.Assistant)
            };

            _mockChatRepository.Setup(r => r.GetActiveThreadForUser(userId))
                .ReturnsAsync(existingThread);

            _mockChatRepository.Setup(r => r.GetMessagesForThread(existingThread.Id))
                .ReturnsAsync(existingMessages);

            // Act
            var result = await _chatService.StartChatAsync(userId);

            // Assert
            Assert.Equal(threadId, result.ThreadId);
            Assert.Equal(existingMessages.Last().Content, result.WelcomeMessage);
            Assert.Equal(existingMessages.Count, result.Messages.Count);
            Assert.Equal(existingMessages[0].Content, result.Messages[0].Content);
            Assert.Equal(existingMessages[0].Role, result.Messages[0].Role);
        }
    }
}