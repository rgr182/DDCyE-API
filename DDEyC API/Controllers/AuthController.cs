using Microsoft.AspNetCore.Mvc;
using DDEyC_API.DataAccess.Services;
using Microsoft.AspNetCore.Authorization;

namespace DDEyC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AuthController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly ILogger<AuthController> _logger;
        public readonly IUserService _usersService;

        public AuthController(ISessionService sessionService, IUserService usersService, ILogger<AuthController> logger)
        {
            _usersService = usersService ?? throw new ArgumentNullException(nameof(usersService));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(string email, string password)
        {
            try
            {
                var userD = await _usersService.GetUserByEmail(email);
                if (userD == null)
                {
                    return Unauthorized("Email does not exist");
                }

                var session = await _sessionService.SaveSession(userD);
                return Ok(session);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Email/password do not match");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during login");
                return Problem("An error occurred during login. Please contact the System Administrator");
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(int sessionId)
        {
            try
            {
                var success = await _sessionService.EndSession(sessionId);
                if (success)
                {
                    return Ok("Session ended successfully");
                }
                else
                {
                    return BadRequest("Failed to end session");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while ending session with ID {SessionId}", sessionId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{sessionId}")]
        public async Task<IActionResult> GetSession(int sessionId)
        {
            try
            {
                var session = await _sessionService.GetSession(sessionId);
                if (session != null)
                {
                    return Ok(session);
                }
                else
                {
                    return NotFound("Session not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving session with ID {SessionId}", sessionId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("validateSession")]
        public async Task<IActionResult> ValidateSession()
        {
            try
            {
                var session = await _sessionService.ValidateSession();
                return Ok(session);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Invalid session");
                return Unauthorized("Invalid session");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while validating session");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
