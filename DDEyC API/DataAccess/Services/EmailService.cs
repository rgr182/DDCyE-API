using DDEyC_API.DataAccess.Models.DTOs;
using System.Net.Mail;
using System.Net;

namespace DDEyC_API.DataAccess.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(EmailRequestDTO request);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendEmailAsync(EmailRequestDTO request)
        {
            try
            {
                // Create a new SMTP client
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(
                        _configuration["SmtpSettings:Email"],
                        _configuration["SmtpSettings:Password"]),
                    EnableSsl = true,
                };

                // Create a new mail message
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["SmtpSettings:Email"]),
                    Subject = request.Subject,
                    Body = request.Body,
                    IsBodyHtml = true,
                };

                // Add the recipient to the mail message
                mailMessage.To.Add(request.To);

                // Send the email asynchronously
                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error sending email: {ex.Message}");
                return false;
            }
        }
    }
}
