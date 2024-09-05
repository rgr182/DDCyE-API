using DDEyC_API.DataAccess.Repositories;
using DDEyC_API.DataAccess.Models.DTOs;
using DDEyC_Auth.DataAccess.Models.Entities;
using System.Security.Cryptography;
using System.Text;

namespace DDEyC_API.DataAccess.Services
{
    public interface IPasswordRecoveryRequestService
    {
        Task<bool> InitiatePasswordRecovery(string email);
    }

    public class PasswordRecoveryRequestService : IPasswordRecoveryRequestService
    {
        private readonly IPasswordRecoveryRequestRepository _passwordRecoveryRequestRepository;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<PasswordRecoveryRequestService> _logger;
        private readonly IConfiguration _configuration;

        public PasswordRecoveryRequestService(
            IPasswordRecoveryRequestRepository passwordRecoveryRequestRepository,
            IEmailService emailService,
            IUserRepository userRepository,
            ILogger<PasswordRecoveryRequestService> logger,
            IConfiguration configuration)
        {
            _passwordRecoveryRequestRepository = passwordRecoveryRequestRepository;
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<bool> InitiatePasswordRecovery(string email)
        {
            try
            {
                // Verificar si el email existe
                var emailExists = await _userRepository.VerifyExistingEmail(email);
                if (!emailExists)
                {
                    _logger.LogInformation("Email not found for password recovery: {Email}", email);
                    return false;
                }

                // Generar el token de recuperación
                var token = GenerateEncryptedToken();

                // Obtener el usuario por correo electrónico
                var user = await _userRepository.GetUserByEmail(email);

                // Crear la solicitud de recuperación de contraseña
                var passwordRecoveryRequest = new PasswordRecoveryRequest
                {
                    UserId = user.UserId,
                    Email = email,
                    Token = token,
                    ExpirationTime = DateTime.UtcNow.AddMinutes(PasswordRecoveryRequest.TokenValidityMinutes)
                };

                // Guardar la solicitud en la base de datos
                await _passwordRecoveryRequestRepository.CreatePasswordRecoveryRequest(passwordRecoveryRequest);

                // Obtener el enlace de base desde appsettings.json
                var recoveryLinkBaseUrl = _configuration["PasswordRecovery:RecoveryLinkBaseUrl"];
                if (string.IsNullOrEmpty(recoveryLinkBaseUrl))
                {
                    _logger.LogError("Recovery link base URL is not configured in appsettings.json");
                    return false;
                }

                // Generar el enlace de recuperación
                var recoveryLink = $"{recoveryLinkBaseUrl}?token={token}";

                // Preparar y enviar el correo electrónico
                var emailRequest = new EmailRequestDTO
                {
                    To = email,
                    Subject = "Password Recovery",
                    Body = $"Please click the following link to reset your password: {recoveryLink}"
                };

                var emailSent = await _emailService.SendEmailAsync(emailRequest);
                if (!emailSent)
                {
                    _logger.LogWarning("Failed to send password recovery email to {Email}", email);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in password recovery for {Email}", email);
                return false;
            }
        }

        private string GenerateEncryptedToken()
        {
            var guid = Guid.NewGuid().ToString();
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(guid));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}
