namespace DDEyC_API.Models
{
    public class MigrationStats
    {
        public int TotalProcessed { get; set; }
        public int Updated { get; set; }
        public int NoChanges { get; set; }
        public int Errors { get; set; }
    }
}