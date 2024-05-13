using DDEyC_API.DataAccess.Context;
using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface IUserRepository
    {
        Task<List<Users>> GetAllUsers();
        Task<Users> GetUser(int id);
        Task<Users> GetUserByEmail(string email);
        Task<Users> AddUser(Users user);
        Task<string> VerifyExistingEmail(string email);
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

        public async Task<Users> GetUserByEmail(string email)
        {
            try
            {
                return await _authContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving user with email {UserEmail}", email);
                throw;
            }
        }

        public async Task<string> VerifyExistingEmail(string email)
        {
            try
            {
                var existingUser = await _authContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                return existingUser != null ? "Email already exists" : "Email available";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while verifying existing email");
                throw;
            }
        }
    }
}
