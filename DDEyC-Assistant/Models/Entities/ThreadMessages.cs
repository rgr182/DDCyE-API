using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DDEyC_Assistant.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }
        public int UserThreadId { get; set; }
        [ForeignKey("UserThreadId")]
        public UserThread UserThread { get; set; }
        public string Content { get; set; }
        public string Role { get; set; }
        public DateTime Timestamp { get; set; }
    }
}