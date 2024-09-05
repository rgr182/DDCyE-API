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

        public PasswordRecoveryRequestService(
            IPasswordRecoveryRequestRepository passwordRecoveryRequestRepository,
            IEmailService emailService,
            IUserRepository userRepository,
            ILogger<PasswordRecoveryRequestService> logger)
        {
            _passwordRecoveryRequestRepository = passwordRecoveryRequestRepository;
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> InitiatePasswordRecovery(string email)
        {
            // Usar el método VerifyExistingEmail del repositorio
            var emailExists = await _userRepository.VerifyExistingEmail(email);
            if (!emailExists)
            {
                return false;  // Usuario no encontrado
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

            // Guardar la solicitud de recuperación en la base de datos
            await _passwordRecoveryRequestRepository.CreatePasswordRecoveryRequest(passwordRecoveryRequest);

            // Generar el enlace de recuperación
            var recoveryLink = $"https://yourapp.com/reset-password?token={token}";

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
