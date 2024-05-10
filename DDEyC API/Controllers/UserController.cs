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
                _logger.LogError(ex, "Error while retrieving all users");
                return StatusCode(500, "Internal server error");
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
                _logger.LogError(ex, "Error while retrieving user with ID {UserId}", id);
                return StatusCode(500, "Internal server error");
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
                _logger.LogError(ex, "Error while retrieving user with email {UserEmail}", email);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegistrationDTO request)
        {
            try
            {
                var user = await _service.Register(request);
                return Ok(new { message = "Registration successful", user.Email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while registering a user");
                return BadRequest(new { message = "Registration failed", error = ex.Message });
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
                    return BadRequest(new { message = "Invalid credentials" });
                }
                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while logging in");
                return BadRequest(new { message = "Login failed", error = ex.Message });
            }
        }
    }
}
