﻿using Microsoft.AspNetCore.Mvc;
using DDEyC_API.DataAccess.Services;
using Microsoft.AspNetCore.Authorization;
using DDEyC_API.DataAccess.Models.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHostEnvironment _hostEnvironment;

        public AuthController(IPasswordRecoveryRequestService passwordRecoveryRequestService,
                              ILogger<AuthController> logger,
                              ISessionService sessionService,
                              IUserService usersService,
                              IHttpContextAccessor httpContextAccessor,
                              IHostEnvironment hostEnvironment)
        {
            _passwordRecoveryRequestService = passwordRecoveryRequestService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _usersService = usersService ?? throw new ArgumentNullException(nameof(usersService));
            _httpContextAccessor = httpContextAccessor;
            _hostEnvironment = hostEnvironment;
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

                var isProd = _hostEnvironment.IsProduction();

                if (isProd || Request.Cookies["prefer-cookies"] != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, userD.UserId.ToString()),
                        new Claim(ClaimTypes.Email, userD.Email),
                        new Claim("session_id", session.SessionId.ToString()),
                        new Claim("token", session.UserToken)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = session.ExpirationDate
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    // Return minimal info when using cookies
                    return Ok(new { authenticated = true, expiresUtc = session.ExpirationDate });
                }

                // Return full session info for JWT/bearer token auth
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during login");
                return Problem("An error occurred during login. Please contact the System Administrator");
            }
        }
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var isProd = _hostEnvironment.IsProduction();

                if (isProd || Request.Cookies["prefer-cookies"] != null)
                {
                    // Handle cookie-based logout
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }

                // Get token either from cookie claims or bearer token
                var token = User.FindFirst("token")?.Value ??
                           HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest("Token is missing.");
                }

                var result = await _sessionService.EndSessionByToken(token);
                if (result)
                {
                    return Ok("Session ended successfully.");
                }

                return BadRequest("Unable to end session.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while ending session");
                return StatusCode(500, "Internal server error");
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
                var isProd = _hostEnvironment.IsProduction();
                string? token = null;

                // Try to get token from cookie claims first
                if (isProd || Request.Cookies["prefer-cookies"] != null)
                {
                    var tokenClaim = User.FindFirst("token");
                    if (tokenClaim != null)
                    {
                        token = tokenClaim.Value;
                        _logger.LogInformation("Using token from cookie claims for validation");
                    }
                }

                // Fall back to bearer token if no cookie token found
                if (string.IsNullOrEmpty(token))
                {
                    token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                    _logger.LogInformation("Using bearer token from Authorization header for validation");
                }

                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized("No token provided");
                }

                var session = await _sessionService.ValidateSession(token);
                var remainingMinutes = (int)(session.ExpirationDate - DateTime.UtcNow).TotalMinutes;

                // If using cookies, refresh the authentication cookie
                if (isProd || Request.Cookies["prefer-cookies"] != null)
                {
                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                new Claim("session_id", session.SessionId.ToString()),
                new Claim("token", token)
            };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = session.ExpirationDate
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);
                }

                return Ok(new
                {
                    Session = session,
                    Message = $"Token expires in {remainingMinutes} minute(s).",
                    AuthType = isProd || Request.Cookies["prefer-cookies"] != null ? "Cookie" : "Bearer"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while validating session");
                return StatusCode(500, "Internal server error");
            }
        }
        //email
        [AllowAnonymous]
        [HttpGet("passwordRecoveryView")]
        public IActionResult PasswordRecoveryView()
        {
            return View("PasswordRecovery");
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
        /// register View
        [AllowAnonymous]
        [HttpGet("registerView")]
        public IActionResult RegisterView()
        {
            return View("RegisterView");
        }
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
                ViewBag.Token = token;
                return View("PasswordReset");
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
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO resetPasswordDto)
        {
            try
            {
                // Verify that the token and new password are not null
                if (string.IsNullOrEmpty(resetPasswordDto.Token) || string.IsNullOrEmpty(resetPasswordDto.NewPassword))
                {
                    ViewData["Error"] = "El token y la nueva contraseña son obligatorios.";
                    return View("PasswordReset");
                }

                // Call the password recovery service
                var result = await _passwordRecoveryRequestService.ResetPassword(resetPasswordDto.Token, resetPasswordDto.NewPassword);

                return Ok(result);
            }
            catch (Exception ex)
            {
                // In case of any exception, catch the error and display a message
                ViewData["Error"] = "Ocurrió un error inesperado: " + ex.Message;
                // You can also log the error if you have a logger
                // _logger.LogError(ex, "Error during password reset");
                return View("PasswordReset");
            }
        }

        [HttpGet("checkSession")]
        public async Task<IActionResult> CheckSession()
        {
            try
            {
                // Obtener el token del encabezado de autorización
                var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized("Token is missing.");
                }

                // Validar si la sesión es válida usando el método ValidateSession del servicio
                var session = await _sessionService.ValidateSession(token);

                // Comparar la fecha de expiración con la fecha actual
                if (session.ExpirationDate > DateTime.UtcNow)
                {
                    return Ok(new { IsSessionValid = true });
                }
                else
                {
                    return Ok(new { IsSessionValid = false });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking session validity.");
                return StatusCode(500, "Internal server error");
            }
        }
        #endregion
    }
}
