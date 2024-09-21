using DDEyC_API.DataAccess.Context;
using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface IUserRepository
    {
        Task<List<Users>> GetAllUsers();
        Task<Users> GetUser(int id);
        Task<Users?> GetUserByEmail(string email);
        Task<Users> AddUser(Users user);
        Task<bool> VerifyExistingEmail(string email);
        Task UpdateUser(Users user); // Method to update the user
    }

    public class UserRepository : IUserRepository
    {
        private readonly AuthContext _authContext;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(ILogger<UserRepository> logger, AuthContext authContext)
        {
            _authContext = authContext;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Implementation of the interface methods

        public async Task<Users> AddUser(Users user)
        {
            try
            {
                _authContext.Users.Add(user);
                await _authContext.SaveChangesAsync();
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while adding user");
                throw;
            }
        }

        public async Task<List<Users>> GetAllUsers()
        {
            try
            {
                return await _authContext.Users.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving all users");
                throw;
            }
        }

        public async Task<Users> GetUser(int id)
        {
            try
            {
                return await _authContext.Users.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving user with ID {UserId}", id);
                throw;
            }
        }

        public async Task<Users?> GetUserByEmail(string email)
        {
            try
            {
                _logger.LogInformation("Retrieving user with email: {UserEmail}", email);
                _logger.LogInformation("------------------------------------------");
                 var user= await _authContext.Users.FirstOrDefaultAsync(u => u.Email == email) ?? null;
                 _logger.LogInformation("User found: {UserFound}", user is not null);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving user with email {UserEmail}", email);
                throw;
            }
        }

        public async Task<bool> VerifyExistingEmail(string email)
        {
            try
            {
                var existingUser = await _authContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                return existingUser is not null; // Returns true if the email exists, false if not
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while verifying existing email");
                throw;
            }
        }

        // New method to update the user
        public async Task UpdateUser(Users user)
        {
            try
            {
                _authContext.Users.Update(user); // Updates the user in the context
                await _authContext.SaveChangesAsync(); // Saves the changes in the database
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating user with ID {UserId}", user.UserId);
                throw;
            }
        }
    }
}
