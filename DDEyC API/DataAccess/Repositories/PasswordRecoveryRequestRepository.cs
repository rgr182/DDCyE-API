using DDEyC_API.DataAccess.Context;
using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface IPasswordRecoveryRequestRepository
    {
        Task<PasswordRecoveryRequest> CreateOrUpdatePasswordRecoveryRequest(PasswordRecoveryRequest passwordRecoveryRequest);
        Task<PasswordRecoveryRequest?> GetPasswordRecoveryRequestByToken(string token);  // Search by token
        Task InvalidateToken(string token);  // Invalidate the token
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

        // Create or update a password recovery request
        public async Task<PasswordRecoveryRequest> CreateOrUpdatePasswordRecoveryRequest(PasswordRecoveryRequest passwordRecoveryRequest)
        {
            try
            {
                // Check if there is already a password recovery request for the user
                var existingRequest = await _authContext.PasswordRecoveryRequest
                    .FirstOrDefaultAsync(r => r.UserId == passwordRecoveryRequest.UserId);

                if (existingRequest != null)
                {
                    // Update the existing record with the new values
                    existingRequest.Token = passwordRecoveryRequest.Token;
                    existingRequest.ExpirationTime = passwordRecoveryRequest.ExpirationTime;
                    existingRequest.Email = passwordRecoveryRequest.Email;

                    _authContext.PasswordRecoveryRequest.Update(existingRequest);
                }
                else
                {
                    // If there is no record for the user, create a new one
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

        // Retrieve the password recovery request by token
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

        // Invalidate the token by updating the expiration date
        public async Task InvalidateToken(string token)
        {
            try
            {
                var request = await _authContext.PasswordRecoveryRequest
                    .FirstOrDefaultAsync(r => r.Token == token);

                if (request != null)
                {
                    request.ExpirationTime = DateTime.UtcNow; // Invalidate the token
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
