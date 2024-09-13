using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DDEyC_Auth.DataAccess.Models.Entities
{
    public class PasswordRecoveryRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PasswordRecoveryRequestId { get; set; }  // Primary key
        public string Email { get; set; }
        public string Token { get; set; }
        public DateTime ExpirationTime { get; set; }
        public int UserId { get; set; }

        // Property for token validity time
        public static int TokenValidityMinutes { get; set; } = 600;  
    }
}
