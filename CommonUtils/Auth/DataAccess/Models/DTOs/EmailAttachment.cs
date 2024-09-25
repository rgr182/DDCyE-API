namespace CommonUtils.AuthDataAccess.Models.DTOs{
     public class EmailAttachment
    {
        public string FileName { get; set; }
        public string ContentId { get; set; }
        public byte[] Content { get; set; }
        public string MimeType { get; set; }
    }
}