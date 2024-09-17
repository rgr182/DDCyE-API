using DDEyC_API.DataAccess.Models.DTOs;
using DDEyC_API.DataAccess.Repositories;
using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace DDEyC_API.DataAccess.Services
{
    public interface IPasswordRecoveryRequestService
    {
        Task<bool> InitiatePasswordRecovery(string email);
        Task<bool> ValidateToken(string token);
        Task<bool> ResetPassword(string token, string newPassword);
    }

    public class PasswordRecoveryRequestService : IPasswordRecoveryRequestService
    {
        private readonly IPasswordRecoveryRequestRepository _passwordRecoveryRequestRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<PasswordRecoveryRequestService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        private readonly string _recoveryLinkBaseUrl;
        private readonly int _tokenValidityMinutes;

        public PasswordRecoveryRequestService(
            IPasswordRecoveryRequestRepository passwordRecoveryRequestRepository,
            IUserRepository userRepository,
            ILogger<PasswordRecoveryRequestService> logger,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _passwordRecoveryRequestRepository = passwordRecoveryRequestRepository;
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));

            _recoveryLinkBaseUrl = _configuration["PasswordRecovery:RecoveryLinkBaseUrl"] ?? throw new ArgumentNullException("RecoveryLinkBaseUrl not found in configuration.");
            _tokenValidityMinutes = int.Parse(_configuration["PasswordRecovery:TokenValidityMinutes"] ?? throw new ArgumentNullException("TokenValidityMinutes not found in configuration."));
        }

        public async Task<bool> ResetPassword(string token, string newPassword)
        {
            try
            {
                // Validate the recovery token
                var recoveryRequest = await _passwordRecoveryRequestRepository.GetPasswordRecoveryRequestByToken(token);
                if (recoveryRequest == null)
                {
                    _logger.LogWarning("Invalid or expired token: {Token}", token);
                    return false;
                }

                // Fetch the user and update the password
                var user = await _userRepository.GetUser(recoveryRequest.UserId);
                if (user == null)
                {
                    _logger.LogError("User not found for ID: {UserId}", recoveryRequest.UserId);
                    return false;
                }

                // Encrypt the new password using BCrypt
                user.Password = HashPassword(newPassword);

                // Update the user's password in the database
                await _userRepository.UpdateUser(user);

                // Invalidate the token after use
                await _passwordRecoveryRequestRepository.InvalidateToken(token);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for token: {Token}", token);
                return false;
            }
        }

        public async Task<bool> InitiatePasswordRecovery(string email)
        {
            try
            {
                // Verify if the email exists in the database
                var emailExists = await _userRepository.VerifyExistingEmail(email);
                if (!emailExists)
                {
                    _logger.LogInformation("Email not found for password recovery: {Email}", email);
                    return false;
                }

                // Generate a recovery token
                var token = GenerateEncryptedToken();
                var user = await _userRepository.GetUserByEmail(email);

                // Create a new password recovery request
                var passwordRecoveryRequest = new PasswordRecoveryRequest
                {
                    UserId = user.UserId,
                    Email = email,
                    Token = token,
                    ExpirationTime = DateTime.UtcNow.AddMinutes(_tokenValidityMinutes) // Token validity from config
                };

                // Save the password recovery request to the database
                await _passwordRecoveryRequestRepository.CreateOrUpdatePasswordRecoveryRequest(passwordRecoveryRequest);

                var recoveryLink = $"{_recoveryLinkBaseUrl}?token={token}";

                // Send the recovery link via email
                await _emailService.SendEmailAsync(new EmailRequestDTO
                {
                    Body = recoveryLink,
                    Subject = "Password Recovery",
                    To = email
                });

                _logger.LogInformation($"Password recovery link for {email}: {recoveryLink}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during password recovery for {Email}", email);
                return false;
            }
        }

        public async Task<bool> ValidateToken(string token)
        {
            try
            {
                var recoveryRequest = await _passwordRecoveryRequestRepository.GetPasswordRecoveryRequestByToken(token);
                if (recoveryRequest == null || recoveryRequest.ExpirationTime < DateTime.UtcNow)
                {
                    _logger.LogWarning("Invalid or expired token: {Token}", token);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while validating the password recovery token: {Token}", token);
                return false;
            }
        }

        private string GenerateEncryptedToken()
        {
            var guid = Guid.NewGuid().ToString();
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(guid));
                var base64String = Convert.ToBase64String(hashedBytes);

                // Use Base64UrlEncoder to make the string URL-safe
                return Base64UrlEncoder.Encode(base64String);
            }
        }

        // Method updated to use BCrypt instead of SHA-256
        private string HashPassword(string password)
        {
            // Use BCrypt to hash the password
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        // Optional method to verify passwords if needed
        public bool VerifyPassword(string enteredPassword, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(enteredPassword, hashedPassword);
        }
    }
}
