using OpenAI.Assistants;

namespace DDEyC_Assistant.Models.Entities
{
     public class RequiredActionEntity
    {
        public string ToolCallId { get; set; }
        public string FunctionName { get; set; }
        public string FunctionArguments { get; set; }
    }
    public class RunEntity
    {
        public string Id { get; set; }
        public RunStatus Status { get; set; }
        public List<RequiredActionEntity> RequiredActions { get; set; }
    }
}