using DDEyC_API.DataAccess.Context;
using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface IUserRepository
    {
        Task<List<User>> GetAllUsers();
        Task<User> GetUser(int id);
        Task<User> GetUserByEmail(string email);
        Task<User> AddUser(User user);
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

        public async Task<User> AddUser(User user)
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

        public async Task<List<User>> GetAllUsers()
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

        public async Task<User> GetUser(int id)
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

        public async Task<User> GetUserByEmail(string email)
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
    }
}
