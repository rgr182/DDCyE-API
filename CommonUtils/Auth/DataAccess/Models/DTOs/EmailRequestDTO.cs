using System.Net.Mail;

namespace CommonUtils.AuthDataAccess.Models.DTOs
{
    public class EmailRequestDTO
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public List<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
    }
}