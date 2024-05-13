using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DDEyC_Auth.DataAccess.Models.Entities
{
    public partial class Sessions
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SessionId { get; set; }
        public int UserId { get; set; } 
        [Required]
        public string UserToken { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime ExpirationDate { get; set; }
    }
}
