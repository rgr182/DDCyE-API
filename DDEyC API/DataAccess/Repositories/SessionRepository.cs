using DDEyC_API.DataAccess.Context;
using DDEyC_Auth.DataAccess.Models.Entities;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface ISessionRepository
    {
        Task<Sessions> GetSession(int sessionId);
        Task<Sessions> AddSession(Sessions session);
        Task DeleteSession(int sessionId);        
    }

    public class SessionRepository : ISessionRepository
    {
        private readonly AuthContext _authContext;
        private readonly ILogger<SessionRepository> _logger;

        public SessionRepository(AuthContext authContext, ILogger<SessionRepository> logger)
        {
            _authContext = authContext;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Sessions> AddSession(Sessions session)
        {
            try
            {
                _authContext.Sessions.Add(session);
                await _authContext.SaveChangesAsync();
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while adding session");
                throw;
            }
        }

        public async Task DeleteSession(int sessionId)
        {
            try
            {
                var session = await _authContext.Sessions.FindAsync(sessionId);
                if (session != null)
                {
                    _authContext.Sessions.Remove(session);
                    await _authContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting session with ID {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<Sessions> GetSession(int sessionId)
        {
            try
            {
                return await _authContext.Sessions.FindAsync(sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving session with ID {SessionId}", sessionId);
                throw;
            }
        }       
    }
}
