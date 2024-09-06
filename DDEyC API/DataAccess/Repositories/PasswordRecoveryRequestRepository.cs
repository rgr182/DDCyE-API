using DDEyC_API.DataAccess.Context;
using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface IPasswordRecoveryRequestRepository
    {
        Task<PasswordRecoveryRequest> CreateOrUpdatePasswordRecoveryRequest(PasswordRecoveryRequest passwordRecoveryRequest);
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

        // Crear o actualizar la solicitud de recuperación de contraseña
        public async Task<PasswordRecoveryRequest> CreateOrUpdatePasswordRecoveryRequest(PasswordRecoveryRequest passwordRecoveryRequest)
        {
            try
            {
                // Verificar si ya existe una solicitud de recuperación de contraseña para el usuario
                var existingRequest = await _authContext.PasswordRecoveryRequest
                    .FirstOrDefaultAsync(r => r.UserId == passwordRecoveryRequest.UserId);

                if (existingRequest != null)
                {
                    // Actualizar el registro existente con los nuevos valores
                    existingRequest.Token = passwordRecoveryRequest.Token;
                    existingRequest.ExpirationTime = passwordRecoveryRequest.ExpirationTime;
                    existingRequest.Email = passwordRecoveryRequest.Email;

                    _authContext.PasswordRecoveryRequest.Update(existingRequest);
                }
                else
                {
                    // Si no existe un registro para el usuario, crear uno nuevo
                    await _authContext.PasswordRecoveryRequest.AddAsync(passwordRecoveryRequest);
                }

                await _authContext.SaveChangesAsync();

                return passwordRecoveryRequest;
            }
            catch (Exception ex)
            {
                // Log the error and throw an exception in case of failure
                _logger.LogError(ex, "Error while creating or updating password recovery request for {Email}", passwordRecoveryRequest.Email);
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
