using DDEyC_Auth.DataAccess.Models.Entities;
using DDEyC_API.DataAccess.Repositories;
using DDEyC_Auth.Utils;

namespace DDEyC_API.DataAccess.Services
{
    public interface ISessionService
    {
        Task<Sessions> SaveSession(Users user);
        Task<bool> EndSession(int sessionId);
        Task<Sessions> GetSession(int sessionId);
        Task<Sessions> ValidateSession(string token);
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

        /// <summary>
        /// Validates the session by checking the JWT token.
        /// </summary>
        /// <returns>An instance of Sessions with the user ID, token, expiration date, and creation date if the session is valid.</returns>
        public async Task<Sessions> ValidateSession(string token)
        {
            try
            {
                // userId, token, expirationDate y creationDate del token JWT
                var (userId, _, expirationDate, creationDate) = _authUtils.GetUserFromToken(token);

               
                return new Sessions
                {
                    UserId = userId,
                    UserToken = token,
                    ExpirationDate = expirationDate,
                    CreationDate = creationDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while validating session");
                throw;
            }
        }
    }
}


