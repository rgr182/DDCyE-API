using System.Net;
using DDEyC_API.DataAccess.Services;
using DDEyC_Auth.DataAccess.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DDEyC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _usersService;
        private readonly ILogger<UserController> _logger;
        private readonly IConfiguration _configuration;

        public UserController(IUserService usersService, ILogger<UserController> logger, IConfiguration configuration)
        {
            _usersService = usersService ?? throw new ArgumentNullException(nameof(usersService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpGet(Name = "GetAllUsers")]        
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _usersService.GetAllUsers();
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
                var user = await _usersService.GetUser(id);
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
                var user = await _usersService.GetUserByEmail(email);
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

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegistrationDTO request)
        {
            try
            {
                var emailExists = await _usersService.VerifyExistingEmail(request.Email);
                _logger.LogInformation("Email exists: {EmailExists}", emailExists);
                if (emailExists)
                {
                    return Conflict(new { errorCode = "EMAIL_ALREADY_REGISTERED" });
                }

                var user = await _usersService.Register(request);
                var loginPageUrl = _configuration["urls:LoginPageUrl"];

                return Ok(new 
                { 
                    message = "REGISTRATION_SUCCESSFUL",
                    user.Email,
                    redirectUrl = loginPageUrl
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { errorCode = "INVALID_INPUT", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while registering a user");
                return StatusCode((int)HttpStatusCode.InternalServerError, new { errorCode = "REGISTRATION_FAILED" });
            }
        }    
    }
}
