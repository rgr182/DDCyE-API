using System.Net.Mail;

namespace DDEyC_API.DataAccess.Models.DTOs
{
    public class EmailRequestDTO
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public List<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
    }

    public class EmailAttachment
    {
        public string FileName { get; set; }
        public string ContentId { get; set; }
        public byte[] Content { get; set; }
        public string MimeType { get; set; }
    }
}