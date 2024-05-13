using DDEyC_Auth.DataAccess.Models.Entities;
using DDEyC_API.DataAccess.Repositories;
using DDEyC_Auth.Utils;
using Microsoft.AspNetCore.Http;

namespace DDEyC_API.DataAccess.Services
{
    public interface ISessionService
    {
        Task<Sessions> SaveSession(Users user);
        Task<bool> EndSession(int sessionId);
        Task<Sessions> GetSession(int sessionId);
        Task<Sessions> ValidateSession();
    }

    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IAuthUtils _authUtils;
        private readonly ILogger<SessionService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionService(ISessionRepository sessionRepository, IAuthUtils authUtils, IHttpContextAccessor httpContextAccessor, ILogger<SessionService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _authUtils = authUtils ?? throw new ArgumentNullException(nameof(authUtils));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Sessions> SaveSession(Users user)
        {
            try
            {
                var session = new Sessions()
                {
                    CreationDate = DateTime.UtcNow,
                    ExpirationDate = DateTime.UtcNow.AddDays(1),
                    UserId = user.UserId,
                    UserToken = _authUtils.GenerateJWT(user)
                };

                return await _sessionRepository.AddSession(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving session for user with ID {UserId}", user.UserId);
                throw;
            }
        }

        public async Task<bool> EndSession(int sessionId)
        {
            try
            {
                await _sessionRepository.DeleteSession(sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while ending session with ID {SessionId}", sessionId);
                return false;
            }
        }

        public async Task<Sessions> GetSession(int sessionId)
        {
            try
            {
                return await _sessionRepository.GetSession(sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving session with ID {SessionId}", sessionId);
                throw;
            }
        }
        public async Task<Sessions> ValidateSession()
        {
            try
            {
                var token = _httpContextAccessor.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

                // Validate the JWT token using the ValidateJWT method of AuthUtils
                if (_authUtils.ValidateJWT(token))
                {
                    // If the token is valid, extract the user ID from the token
                    var userId = _authUtils.GetUserIdFromToken(token);

                    // Return an instance of Sessions with the user ID and the JWT token
                    return new Sessions
                    {
                        UserId = userId,
                        UserToken = token
                    };
                }
                else
                {
                    throw new UnauthorizedAccessException("Invalid JWT token.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while validating session");
                throw;
            }
        }
    }
}
