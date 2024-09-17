namespace DDEyC_Assistant.Models.DTOs
{
    public class UserRegistrationDTO
    {
        public required string Name { get; set; } = string.Empty;
        public required string Surname { get; set; } = string.Empty;
        public required string Email { get; set; } = string.Empty;
        public required string Password { get; set; } = string.Empty;
    }
    public class EmailLoginDTO
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
    public class RequestOtpDTO
    {
        public required string Email { get; set; }
        public bool IsReset { get; set; } = false;
    }
    public class PasswordResetDTO
    {
        public required string NewPassword { get; set; }
        public required string ConfirmPassword { get; set; }
    }
}
