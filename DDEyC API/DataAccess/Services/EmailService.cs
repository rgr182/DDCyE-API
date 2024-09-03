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
        private readonly string _smtpClient;
        private readonly string _smtpEmail;
        private readonly string _smtpPassword;

        public EmailService(IConfiguration configuration)
        {
            _smtpClient = configuration["SmtpSettings:SMTPClient"];
            _smtpEmail = configuration["SmtpSettings:Email"];
            _smtpPassword = configuration["SmtpSettings:Password"];
        }

        public async Task<bool> SendEmailAsync(EmailRequestDTO request)
        {
            try
            {
                // Crear un nuevo cliente SMTP
                var smtpClient = new SmtpClient(_smtpClient)
                {
                    Port = 587,
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_smtpEmail, _smtpPassword),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                // Crear un nuevo mensaje de correo
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpEmail),
                    Subject = request.Subject,
                    Body = request.Body,
                    IsBodyHtml = true,
                };

                // Agregar el destinatario al mensaje de correo
                mailMessage.To.Add(request.To);

                // Enviar el correo de forma asíncrona
                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                // Registrar la excepción
                Console.WriteLine($"Error al enviar el correo: {ex.Message}");
                return false;
            }
        }
    }
}
