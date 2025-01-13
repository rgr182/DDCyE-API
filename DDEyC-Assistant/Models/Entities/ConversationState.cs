namespace DDEyC_Assistant.Models
{
    public enum ConversationState
    {
        Idle,
        Processing,
        Error
    }

    public class ConversationStateEntity
    {
        public int Id { get; set; }
        public string ConversationId { get; set; }
        public DateTime LastOperation { get; set; }
        public ConversationState State { get; set; }
        public string? CurrentRunId { get; set; }
    }
}