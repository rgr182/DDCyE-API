// TestData/TestDataFactory.cs
using DDEyC_Assistant.Models;
using DDEyC_Assistant.Models.Entities;
using OpenAI.Assistants;

namespace DDEyC_Assistant.Tests.TestData
{
    public static class TestDataFactory
    {
        public static UserThread CreateUserThread(int id = 1, int userId = 1, string threadId = "thread_123")
        {
            return new UserThread
            {
                Id = id,
                UserId = userId,
                ThreadId = threadId,
                LastUsed = DateTime.UtcNow,
                IsActive = true
            };
        }

        public static Message CreateMessage(
            int id = 1, 
            int userThreadId = 1, 
            string content = "Test message", 
            MessageRole role = MessageRole.User)
        {
            return new Message
            {
                Id = id,
                UserThreadId = userThreadId,
                Content = content,
                Role = role.ToString(),
                Timestamp = DateTime.UtcNow
            };
        }

        public static RunEntity CreateRunRequiringAction(
            string id = "run_123",
            string functionName = "get_job_listings",
            string functionArgs = "{}")
        {
            var requiredAction = new RequiredActionEntity
            {
                ToolCallId = "tool_123",
                FunctionName = functionName,
                FunctionArguments = functionArgs
            };

            return new RunEntity
            {
                Id = id,
                Status = "requires_action",
                RequiredActions = new List<RequiredActionEntity> { requiredAction }
            };
        }

        public static RunEntity CreateRunInProgress(string id = "run_123")
        {
            return new RunEntity
            {
                Id = id,
                Status = "in_progress",
                RequiredActions = null
            };
        }

        public static RunEntity CreateRunCompleted(string id = "run_123")
        {
            return new RunEntity
            {
                Id = id,
                Status = "completed",
                RequiredActions = null
            };
        }

        public static RunEntity CreateRunFailed(string id = "run_123")
        {
            return new RunEntity
            {
                Id = id,
                Status = "failed",
                RequiredActions = null
            };
        }

        public static MessageEntity CreateAssistantMessageEntity(string content = "Test response")
        {
            return new MessageEntity
            {
                Content = content
            };
        }
    }
}