namespace DDEyC_API.DataAccess.Models.DTOs
{
    public class ResetPasswordDTO
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }
}
