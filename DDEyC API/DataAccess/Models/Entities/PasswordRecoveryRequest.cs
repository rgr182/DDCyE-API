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

        // Propiedad para el tiempo de validez del token
        public static int TokenValidityMinutes { get; set; } = 30;  // Valor por defecto de 30 minutos
    }
}
