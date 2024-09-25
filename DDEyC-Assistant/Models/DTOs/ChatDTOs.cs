namespace DDEyC_Assistant.Models.DTOs
{
    public class ChatStartResultDto
    {
        public string ThreadId { get; set; }
        public string WelcomeMessage { get; set; }
        public List<MessageDto> Messages { get; set; }
    }

    public class ChatRequestDto
    {
        public string ThreadId { get; set; }
        public string UserMessage { get; set; }
    }

    public class ChatResponseDto
    {
        public string ThreadId { get; set; }
        public string Response { get; set; }
    }

    public class MessageDto
    {
        public string Content { get; set; }
        public string Role { get; set; }
        public DateTime Timestamp { get; set; }
    }
    public class UserThreadDto
    {
        public int Id { get; set; }
        public string ThreadId { get; set; }
        public DateTime LastUsed { get; set; }
        public bool IsActive { get; set; }
    }
}