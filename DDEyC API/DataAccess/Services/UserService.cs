using DDEyC_API.DataAccess.Repositories;
using DDEyC_Auth.DataAccess.Models.DTOs;
using DDEyC_Auth.DataAccess.Models.Entities;

namespace DDEyC_API.DataAccess.Services
{
    public interface IUserService
    {
        Task<List<User>> GetAllUsers();
        Task<User> GetUser(int id);
        Task<User> GetUserByEmail(string email);
        Task<User> Register(UserRegistrationDTO request);
        Task<string> Login(string email, string password);
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepository repository, IConfiguration configuration, ILogger<UserService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<User>> GetAllUsers()
        {
            try
            {
                return await _repository.GetAllUsers();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todos los usuarios");
                throw;
            }
        }

        public async Task<User> GetUser(int id)
        {
            try
            {
                return await _repository.GetUser(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el usuario con ID {UserId}", id);
                throw;
            }
        }

        public async Task<User> Register(UserRegistrationDTO registrationDTO)
        {
            try
            {
                var existingUser = await _repository.GetUserByEmail(registrationDTO.Email);
                if (existingUser != null)
                {
                    throw new Exception("Ya existe una cuenta con este correo electrónico.");
                }

                string passwordHash = GeneratePasswordHash(registrationDTO.Password);
                User user = new User
                {
                    Name = registrationDTO.Name,
                    Email = registrationDTO.Email,
                    Password = passwordHash,
                };

                return await _repository.AddUser(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar un usuario");
                throw;
            }
        }

        public async Task<string> Login(string email, string password)
        {
            try
            {
                var user = await _repository.GetUserByEmail(email);
                if (user == null)
                {
                    return null;
                }
                if (!CheckHash(user, password))
                {
                    throw new Exception("Credenciales inválidas");
                }
                
                string token = GenerateAuthToken(user);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar sesión");
                throw;
            }
        }

        public async Task<User> GetUserByEmail(string email)
        {
            try
            {
                return await _repository.GetUserByEmail(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el usuario con correo electrónico {UserEmail}", email);
                throw;
            }
        }

        private string GeneratePasswordHash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private string GenerateAuthToken(User user)
        {
            string token = Guid.NewGuid().ToString();
            string tokenHash = BCrypt.Net.BCrypt.HashPassword(token);
            // Aquí podrías almacenar el token en la base de datos o en una caché para su posterior verificación.
            return token;
        }

        private bool CheckHash(User user, string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, user.Password);
        }
    }
}
