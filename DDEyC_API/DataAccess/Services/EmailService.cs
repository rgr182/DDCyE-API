using DDEyC_API.DataAccess.Models.DTOs;
using System.Net.Mail;
using System.Net;
using System.Net.Mime;

namespace DDEyC_API.DataAccess.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(EmailRequestDTO request);
    }

    public class EmailService : IEmailService
    {
        private readonly string _smtpClient;
        private readonly string _smtpEmail;
        private readonly string _smtpPassword;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _smtpClient = configuration["SmtpSettings:SMTPClient"];
            _smtpEmail = configuration["SmtpSettings:Email"];
            _smtpPassword = configuration["SmtpSettings:Password"];
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(EmailRequestDTO request)
        {
            try
            {
                var smtpClient = new SmtpClient(_smtpClient)
                {
                    Port = 587,
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_smtpEmail, _smtpPassword),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpEmail),
                    Subject = request.Subject,
                    Body = request.Body,
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(request.To);

                foreach (var attachment in request.Attachments)
                {
                    var linkedResource = new LinkedResource(new MemoryStream(attachment.Content), attachment.MimeType)
                    {
                        ContentId = attachment.ContentId
                    };
                    var view = AlternateView.CreateAlternateViewFromString(mailMessage.Body, null, MediaTypeNames.Text.Html);
                    view.LinkedResources.Add(linkedResource);
                    mailMessage.AlternateViews.Add(view);
                }             

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email");
                return false;
            }
        }
    }
}