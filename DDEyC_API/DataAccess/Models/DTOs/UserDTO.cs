namespace DDEyC_Auth.DataAccess.Models.DTOs
{  
    public class EmailLoginDTO
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}
