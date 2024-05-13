using Microsoft.AspNetCore.Mvc;
using DDEyC_API.DataAccess.Services;

namespace DDEyC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly ILogger<AuthController> _logger;
        public readonly  IUserService _usersService;

        public AuthController(ISessionService sessionService, IUserService usersService, ILogger<AuthController> logger)
        {
            _usersService = usersService ?? throw new ArgumentNullException(nameof(usersService));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(string email, string password)
        {
            try
            {
                var userD = await _usersService.GetUserByEmail(email);
                if (userD == null)
                {
                    return Unauthorized("El email no existe");
                }

                var session = await _sessionService.SaveSession(userD);
                return Ok(session);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("El email/contraseña no coinciden");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un problema durante el inicio de sesión");
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
    }
}
