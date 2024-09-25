using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DDEyC_Assistant.Models
{
    public class UserThread
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public string ThreadId { get; set; }
        public DateTime LastUsed { get; set; }
        public bool IsActive { get; set; }
    }

    
}