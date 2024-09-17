 using DDEyC_API.DataAccess.Models.DTOs;
using DDEyC_API.DataAccess.Repositories;
using DDEyC_Auth.DataAccess.Models.Entities;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
        private readonly IWebHostEnvironment _webHostEnvironment;

        private readonly string _recoveryLinkBaseUrl;
        private readonly int _tokenValidityMinutes;
        private readonly string _emailTemplatePath;

        public PasswordRecoveryRequestService(
            IPasswordRecoveryRequestRepository passwordRecoveryRequestRepository,
            IUserRepository userRepository,
            ILogger<PasswordRecoveryRequestService> logger,
            IConfiguration configuration,
            IEmailService emailService,
            IWebHostEnvironment webHostEnvironment)
        {
            _passwordRecoveryRequestRepository = passwordRecoveryRequestRepository;
            _userRepository = userRepository;
            _logger = logger;
            _configuration = configuration;
            _emailService = emailService;
            _webHostEnvironment = webHostEnvironment;

            _recoveryLinkBaseUrl = _configuration["PasswordRecovery:RecoveryLinkBaseUrl"];
            _tokenValidityMinutes = int.Parse(_configuration["PasswordRecovery:TokenValidityMinutes"]);
            _emailTemplatePath = _configuration["PasswordRecovery:EmailTemplatePath"];
        }

public async Task<bool> InitiatePasswordRecovery(string email)
        {
            try
            {
                var emailExists = await _userRepository.VerifyExistingEmail(email);
                if (!emailExists)
                {
                    _logger.LogInformation("Email not found for password recovery: {Email}", email);
                    return false;
                }

                var token = GenerateEncryptedToken();
                var user = await _userRepository.GetUserByEmail(email);

                var passwordRecoveryRequest = new PasswordRecoveryRequest
                {
                    UserId = user.UserId,
                    Email = email,
                    Token = token,
                    ExpirationTime = DateTime.UtcNow.AddMinutes(_tokenValidityMinutes)
                };

                await _passwordRecoveryRequestRepository.CreateOrUpdatePasswordRecoveryRequest(passwordRecoveryRequest);

                var recoveryLink = $"{_recoveryLinkBaseUrl}?token={token}";

                string emailBody = await LoadAndFormatEmailTemplate(recoveryLink);

                await _emailService.SendEmailAsync(new EmailRequestDTO
                {
                    Body = emailBody,
                    Subject = "Password Recovery",
                    To = email
                });

                _logger.LogInformation("Password recovery link sent to {Email}", email);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during password recovery for {Email}", email);
                return false;
            }
        }

        private async Task<string> LoadAndFormatEmailTemplate(string recoveryLink)
        {
            try
            {
                string templatePath;
                if (Path.IsPathRooted(_emailTemplatePath))
                {
                    templatePath = _emailTemplatePath;
                }
                else
                {
                    templatePath = Path.Combine(_webHostEnvironment.ContentRootPath, _emailTemplatePath.TrimStart('~').TrimStart('/'));
                }

                _logger.LogInformation("Attempting to load email template from: {TemplatePath}", templatePath);

                if (!File.Exists(templatePath))
                {
                    _logger.LogError("Email template file not found at {TemplatePath}", templatePath);
                    throw new FileNotFoundException($"Email template file not found at {templatePath}");
                }

                var templateContent = await File.ReadAllTextAsync(templatePath);
                return templateContent.Replace("{reset_link}", recoveryLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading or formatting email template");
                throw;
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

        public async Task<bool> ResetPassword(string token, string newPassword)
        {
            try
            {
                var recoveryRequest = await _passwordRecoveryRequestRepository.GetPasswordRecoveryRequestByToken(token);
                if (recoveryRequest == null)
                {
                    _logger.LogWarning("Invalid or expired token: {Token}", token);
                    return false;
                }

                var user = await _userRepository.GetUser(recoveryRequest.UserId);
                if (user == null)
                {
                    _logger.LogError("User not found for ID: {UserId}", recoveryRequest.UserId);
                    return false;
                }

                user.Password = HashPassword(newPassword);

                await _userRepository.UpdateUser(user);

                await _passwordRecoveryRequestRepository.InvalidateToken(token);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for token: {Token}", token);
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
                return Base64UrlEncoder.Encode(base64String);
            }
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}
