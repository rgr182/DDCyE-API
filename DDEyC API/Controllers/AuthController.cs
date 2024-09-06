using Microsoft.AspNetCore.Mvc;
using DDEyC_API.DataAccess.Services;
using Microsoft.AspNetCore.Authorization;

namespace DDEyC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly IPasswordRecoveryRequestService _passwordRecoveryRequestService;
        private readonly ILogger<AuthController> _logger;
        private readonly ISessionService _sessionService;
        private readonly IUserService _usersService;

        public AuthController(IPasswordRecoveryRequestService passwordRecoveryRequestService,
                              ILogger<AuthController> logger,
                              ISessionService sessionService,
                              IUserService usersService)
        {
            _passwordRecoveryRequestService = passwordRecoveryRequestService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _usersService = usersService ?? throw new ArgumentNullException(nameof(usersService));
        }

        #region API Endpoints

        /// <summary>
        /// Login method to authenticate users.
        /// </summary>
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

        /// <summary>
        /// Retrieves a session by ID.
        /// </summary>
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

        /// <summary>
        /// Validates a session token.
        /// </summary>
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

        /// <summary>
        /// Handles password recovery requests.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("passwordRecovery")]
        public async Task<IActionResult> PasswordRecovery([FromBody] string email)
        {
            try
            {
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

        #endregion

        #region MVC Views

        /// <summary>
        /// Validates the recovery token for password reset.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("validateRecoveryToken")]
        public async Task<IActionResult> ValidateRecoveryToken(string token)
        {
            var isValidToken = await _passwordRecoveryRequestService.ValidateToken(token);

            if (isValidToken)
            {
                ViewBag.Token = token;  // Pass the token to the view.
                return View("PasswordReset");  // Load the password reset view.
            }
            else
            {
                ViewData["Error"] = "Invalid or expired password recovery token.";
                return View("Error");
            }
        }



        /// <summary>
        /// Resets the password.
        /// </summary>
        [HttpPost("resetPassword")]
        public async Task<IActionResult> ResetPassword(string token, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewData["Error"] = "Passwords do not match.";
                return View("PasswordReset");
            }

            var result = await _passwordRecoveryRequestService.ResetPassword(token, newPassword);

            if (result)
            {
                ViewData["Success"] = "Your password has been successfully reset.";
                return View("PasswordReset"); // You can also redirect to another success page if needed.
            }
            else
            {
                ViewData["Error"] = "Error resetting password.";
                return View("PasswordReset");
            }
        }

        #endregion
    }
}
