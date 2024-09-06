using DDEyC_API.DataAccess.Context;
using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface IPasswordRecoveryRequestRepository
    {
        Task<PasswordRecoveryRequest> CreatePasswordRecoveryRequest(PasswordRecoveryRequest passwordRecoveryRequest);
        Task<PasswordRecoveryRequest?> GetPasswordRecoveryRequestByToken(string token);  // Buscar por token
        Task InvalidateToken(string token);  // Invalidar el token
    }

    public class PasswordRecoveryRequestRepository : IPasswordRecoveryRequestRepository
    {
        private readonly AuthContext _authContext;
        private readonly ILogger<PasswordRecoveryRequestRepository> _logger;

        public PasswordRecoveryRequestRepository(AuthContext authContext, ILogger<PasswordRecoveryRequestRepository> logger)
        {
            _authContext = authContext;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Crear la solicitud de recuperación de contraseña
        public async Task<PasswordRecoveryRequest> CreatePasswordRecoveryRequest(PasswordRecoveryRequest passwordRecoveryRequest)
        {
            try
            {
                // Truncate the table to remove all records and reset the identity seed
                await _authContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE PasswordRecoveryRequest");

                // Add the new password recovery request record
                _authContext.PasswordRecoveryRequest.Add(passwordRecoveryRequest);
                await _authContext.SaveChangesAsync();

                return passwordRecoveryRequest;
            }
            catch (Exception ex)
            {
                // Log the error and throw an exception in case of failure
                _logger.LogError(ex, "Error while creating password recovery request for {Email}", passwordRecoveryRequest.Email);
                throw;
            }
        }


        // Obtener la solicitud de recuperación por token
        public async Task<PasswordRecoveryRequest?> GetPasswordRecoveryRequestByToken(string token)
        {
            try
            {
                return await _authContext.PasswordRecoveryRequest
                    .FirstOrDefaultAsync(r => r.Token == token && r.ExpirationTime > DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving password recovery request with token {Token}", token);
                throw;
            }
        }

        // Invalidar el token al actualizar la fecha de expiración
        public async Task InvalidateToken(string token)
        {
            try
            {
                var request = await _authContext.PasswordRecoveryRequest
                    .FirstOrDefaultAsync(r => r.Token == token);

                if (request != null)
                {
                    request.ExpirationTime = DateTime.UtcNow; // Invalidar el token
                    await _authContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while invalidating token {Token}", token);
                throw;
            }
        }
    }
}
