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
        private readonly IUserService _usersService;
        private readonly IPasswordRecoveryRequestService _passwordRecoveryRequestService;

        public AuthController(ISessionService sessionService,
                              IUserService usersService,
                              IPasswordRecoveryRequestService passwordRecoveryRequestService,
                              ILogger<AuthController> logger)
        {
            _usersService = usersService ?? throw new ArgumentNullException(nameof(usersService));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _passwordRecoveryRequestService = passwordRecoveryRequestService ?? throw new ArgumentNullException(nameof(passwordRecoveryRequestService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(string email, string password)
        {
            try
            {
                var userD = await _usersService.Login(email, password);
                if (userD == null)
                {
                    return Unauthorized("Email/password do not match");
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
        [AllowAnonymous]
        public async Task<IActionResult> ValidateSession()
        {
            try
            {
                var session = await _sessionService.ValidateSession();

                // Calculate remaining minutes of the token
                var remainingMinutes = (int)(session.ExpirationDate - DateTime.UtcNow).TotalMinutes;

                // Construct the message
                var message = $"Token expires in {remainingMinutes} minute(s).";

                // Return Ok response with the session and message
                return Ok(new { Session = session, Message = message });
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

        [AllowAnonymous]
        [HttpPost("passwordRecovery")]
        public async Task<IActionResult> PasswordRecovery([FromBody] string email)
        {
            try
            {
                // Aquí llamamos al servicio en lugar del repositorio directamente
                var result = await _passwordRecoveryRequestService.InitiatePasswordRecovery(email);
                if (result)
                {
                    return Ok("Password recovery instructions have been sent to your email.");
                }
                else
                {
                    return NotFound("User with the provided email does not exist.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during password recovery for {Email}", email);
                return StatusCode(500, "An error occurred during password recovery.");
            }
        }
    }
}
