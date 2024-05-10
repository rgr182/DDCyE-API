using DDEyC_API.DataAccess.Repositories;
using DDEyC_Auth.DataAccess.Models.Entities;

namespace DDEyC_API.DataAccess.Services
{
    public interface ISessionService
    {
        Task<string> StartSession(int userId);
        Task<bool> EndSession(int sessionId);
        Task<Sessions> GetSession(int sessionId);
        Task<bool> ValidateSession(int sessionId, string token);
    }

    public class SessionService : ISessionService
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly ILogger<SessionService> _logger;

        public SessionService(ISessionRepository sessionRepository, ILogger<SessionService> logger)
        {
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> StartSession(int userId)
        {
            try
            {
                // Crear una nueva sesión
                var session = new Sessions
                {
                    UserId = userId,
                    UserToken = Guid.NewGuid().ToString(), // Usar un token único para la sesión
                    ExpirationDate = DateTime.UtcNow.AddHours(1) // Ejemplo: expira en una hora
                };

                await _sessionRepository.AddSession(session);

                return session.UserToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting session for user with ID {UserId}", userId);
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

        public async Task<bool> ValidateSession(int sessionId, string token)
        {
            try
            {
                var session = await _sessionRepository.GetSession(sessionId);
                if (session == null || session.UserToken != token || session.ExpirationDate < DateTime.UtcNow)
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while validating session with ID {SessionId}", sessionId);
                return false;
            }
        }
    }
}
