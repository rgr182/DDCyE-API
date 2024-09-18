using DDEyC_API.DataAccess.Models.DTOs;
using DDEyC_API.DataAccess.Repositories;
using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using PreMailer.Net;

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
        private readonly IHostEnvironment _hostEnvironment;

        private readonly string _recoveryLinkBaseUrl;
        private readonly int _tokenValidityMinutes;
        private readonly string _emailTemplatePath;
        private readonly string _emailCssPath;
        private readonly Dictionary<string, string> _imagePathsConfig;

        public PasswordRecoveryRequestService(
            IPasswordRecoveryRequestRepository passwordRecoveryRequestRepository,
            IUserRepository userRepository,
            ILogger<PasswordRecoveryRequestService> logger,
            IConfiguration configuration,
            IEmailService emailService,
            IWebHostEnvironment webHostEnvironment,
            IHostEnvironment hostEnvironment)
        {
            _passwordRecoveryRequestRepository = passwordRecoveryRequestRepository;
            _userRepository = userRepository;
            _logger = logger;
            _configuration = configuration;
            _emailService = emailService;
            _webHostEnvironment = webHostEnvironment;
            _hostEnvironment = hostEnvironment;

            _recoveryLinkBaseUrl = _configuration["PasswordRecovery:RecoveryLinkBaseUrl"];
            _tokenValidityMinutes = int.Parse(_configuration["PasswordRecovery:TokenValidityMinutes"]);
            _emailTemplatePath = _configuration["PasswordRecovery:EmailTemplatePath"];
            _emailCssPath = _configuration["PasswordRecovery:EmailCssPath"];
            _imagePathsConfig = _configuration.GetSection("PasswordRecovery:EmailTemplateImagePaths").Get<Dictionary<string, string>>();
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

                var emailRequest = new EmailRequestDTO
                {
                    Body = emailBody,
                    Subject = "Password Recovery",
                    To = email,
                    Attachments = await GetEmailAttachments()
                };

                await _emailService.SendEmailAsync(emailRequest);

                _logger.LogInformation("Password recovery link sent to {Email}", email);

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

        private async Task<string> LoadAndFormatEmailTemplate(string recoveryLink)
        {
            try
            {
                string templatePath = ResolveTemplatePath();
                string cssPath = ResolveCssPath();

                _logger.LogInformation("Attempting to load email template from: {TemplatePath}", templatePath);
                _logger.LogInformation("Attempting to load CSS from: {CssPath}", cssPath);

                if (!File.Exists(templatePath))
                {
                    _logger.LogError("Email template file not found at {TemplatePath}", templatePath);
                    throw new FileNotFoundException($"Email template file not found at {templatePath}");
                }

                if (!File.Exists(cssPath))
                {
                    _logger.LogError("CSS file not found at {CssPath}", cssPath);
                    throw new FileNotFoundException($"CSS file not found at {cssPath}");
                }

                var templateContent = await File.ReadAllTextAsync(templatePath);
                var cssContent = await File.ReadAllTextAsync(cssPath);

                // Replace the placeholder with the actual recovery link
                templateContent = templateContent.Replace("{reset_link}", recoveryLink);

                // Add the CSS to the template
                templateContent = templateContent.Replace("/* Styles will be inlined at runtime */", cssContent);

                // Use PreMailer.Net to inline the styles
                var result = PreMailer.Net.PreMailer.MoveCssInline(templateContent);

                return result.Html;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading or formatting email template");
                throw;
            }
        }

        private string ResolveTemplatePath()
        {
            // Try WebRootPath first
            if (!string.IsNullOrEmpty(_webHostEnvironment.WebRootPath))
            {
                return Path.Combine(_webHostEnvironment.WebRootPath, _emailTemplatePath.TrimStart('~').TrimStart('/'));
            }

            // If WebRootPath is null, try ContentRootPath
            if (!string.IsNullOrEmpty(_webHostEnvironment.ContentRootPath))
            {
                return Path.Combine(_webHostEnvironment.ContentRootPath, _emailTemplatePath.TrimStart('~').TrimStart('/'));
            }

            // If both are null, fall back to IHostEnvironment
            return Path.Combine(_hostEnvironment.ContentRootPath, _emailTemplatePath.TrimStart('~').TrimStart('/'));
        }

        private string ResolveCssPath()
        {
            // Similar to ResolveTemplatePath, but for the CSS file
            if (!string.IsNullOrEmpty(_webHostEnvironment.WebRootPath))
            {
                return Path.Combine(_webHostEnvironment.WebRootPath, _emailCssPath.TrimStart('~').TrimStart('/'));
            }

            if (!string.IsNullOrEmpty(_webHostEnvironment.ContentRootPath))
            {
                return Path.Combine(_webHostEnvironment.ContentRootPath, _emailCssPath.TrimStart('~').TrimStart('/'));
            }

            return Path.Combine(_hostEnvironment.ContentRootPath, _emailCssPath.TrimStart('~').TrimStart('/'));
        }

        private async Task<List<EmailAttachment>> GetEmailAttachments()
        {
            var attachments = new List<EmailAttachment>();

            foreach (var imagePath in _imagePathsConfig)
            {
                attachments.Add(await CreateAttachment(imagePath.Value, $"logo-{imagePath.Key.ToLower()}"));
            }

            // Add the footer logo (assuming it uses the Municipal logo)
            if (_imagePathsConfig.TryGetValue("Municipal", out var footerLogoPath))
            {
                attachments.Add(await CreateAttachment(footerLogoPath, "footer-logo"));
            }

            return attachments;
        }

        private async Task<EmailAttachment> CreateAttachment(string filePath, string contentId)
        {
            var resolvedPath = ResolveFilePath(filePath);
            var content = await File.ReadAllBytesAsync(resolvedPath);
            return new EmailAttachment
            {
                FileName = Path.GetFileName(resolvedPath),
                ContentId = contentId,
                Content = content,
                MimeType = GetMimeType(resolvedPath)
            };
        }

        private string ResolveFilePath(string configPath)
        {
            // Remove the leading "~/" if present
            var relativePath = configPath.TrimStart('~').TrimStart('/');

            var possiblePaths = new[]
            {
                Path.Combine(_webHostEnvironment.WebRootPath ?? "", relativePath),
                Path.Combine(_webHostEnvironment.ContentRootPath ?? "", relativePath),
                Path.Combine(_hostEnvironment.ContentRootPath, relativePath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath)
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException($"Could not find file: {configPath}");
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream",
            };
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