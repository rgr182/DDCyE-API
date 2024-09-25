namespace DDEyC_Auth.DataAccess.Models.DTOs
{
    public class UserRegistrationDTO
    {
        public required string Name { get; set; } = string.Empty;
        public required string Email { get; set; } = string.Empty;
        public required string Password { get; set; } = string.Empty;
    }
}
