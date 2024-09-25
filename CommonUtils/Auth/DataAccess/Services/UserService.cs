using CommonUtils.AuthDataAccess.Repositories;
using DDEyC_Auth.DataAccess.Models.DTOs;
using DDEyC_Auth.DataAccess.Models.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CommonUtils.AuthDataAccess.Services
{
    public interface IUserService
    {
        Task<List<Users>> GetAllUsers();
        Task<Users> GetUser(int id);
        Task<Users> GetUserByEmail(string email);
        Task<Users> Register(UserRegistrationDTO request);
        Task<Users> Login(string email, string password);
        Task<string> VerifyExistingEmail(string email);
    }

    public class UserService : IUserService
    {
        #region Private Fields

        private readonly IUserRepository _userRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;

        #endregion

        #region Constructor

        public UserService(IUserRepository userRepository, ISessionRepository sessionRepository, IConfiguration configuration, ILogger<UserService> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Public Methods

        public async Task<List<Users>> GetAllUsers()
        {
            try
            {
                return await _userRepository.GetAllUsers();
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
                return await _userRepository.GetUser(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving user with ID {UserId}", id);
                throw;
            }
        }

        public async Task<Users> Register(UserRegistrationDTO registrationDTO)
        {
            try
            {
                var existingUser = await _userRepository.GetUserByEmail(registrationDTO.Email);
                if (existingUser != null)
                {
                    throw new Exception("An account with this email already exists.");
                }

                string passwordHash = GeneratePasswordHash(registrationDTO.Password);
                Users user = new Users
                {
                    Name = registrationDTO.Name,
                    Email = registrationDTO.Email,
                    Password = passwordHash,
                };

                return await _userRepository.AddUser(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while registering a user");
                throw;
            }
        }

        public async Task<Users> Login(string email, string password)
        {
            try
            {
                var user = await _userRepository.GetUserByEmail(email);
                if (user == null || !CheckHash(user, password))
                {
                    throw new UnauthorizedAccessException("Invalid credentials");
                }

                // Return the user object after successful login
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while logging in");
                throw;
            }
        }

        public async Task<Users> GetUserByEmail(string email)
        {
            try
            {
                return await _userRepository.GetUserByEmail(email);
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
                var existingUser = await _userRepository.GetUserByEmail(email);
                return existingUser != null ? "Email already exists" : "Email available";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while verifying existing email");
                throw;
            }
        }

        #endregion

        #region Private Methods

        private string GeneratePasswordHash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private bool CheckHash(Users user, string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, user.Password);
        }

        #endregion
    }
}
