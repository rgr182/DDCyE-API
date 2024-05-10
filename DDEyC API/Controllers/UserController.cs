using DDEyC_API.DataAccess.Services;
using DDEyC_Auth.DataAccess.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DDEyC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _service;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService service, ILogger<UserController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet(Name = "All")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _service.GetAllUsers();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todos los usuarios");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _service.GetUser(id);
                if (user == null)
                {
                    return NotFound("User not found");
                }
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el usuario con ID {UserId}", id);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        [HttpGet("email/{email}")]
        [Authorize]
        public async Task<IActionResult> GetUserByEmail(string email)
        {
            try
            {
                var user = await _service.GetUserByEmail(email);
                if (user == null)
                {
                    return NotFound();
                }
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el usuario con email {UserEmail}", email);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegistrationDTO request)
        {
            try
            {
                var user = await _service.Register(request);
                return Ok(new { message = "Registro exitoso", user.Email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar un usuario");
                return BadRequest(new { message = "Registro fallido", error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(EmailLoginDTO request)
        {
            try
            {
                var token = await _service.Login(request.Email, request.Password);
                if (token == null)
                {
                    return BadRequest(new { message = "Credenciales inválidas" });
                }
                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar sesión");
                return BadRequest(new { message = "Inicio de sesión fallido", error = ex.Message });
            }
        }
    }
}
