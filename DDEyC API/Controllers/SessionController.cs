using Microsoft.AspNetCore.Mvc;
using DDEyC_API.DataAccess.Services;

namespace DDEyC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly ILogger<SessionController> _logger;

        public SessionController(ISessionService sessionService, ILogger<SessionController> logger)
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartSession(int userId)
        {
            try
            {
                var token = await _sessionService.StartSession(userId);
                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting session for user with ID {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("end")]
        public async Task<IActionResult> EndSession(int sessionId)
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

        [HttpGet("validate")]
        public async Task<IActionResult> ValidateSession(int sessionId, string token)
        {
            try
            {
                var isValid = await _sessionService.ValidateSession(sessionId, token);
                return Ok(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while validating session with ID {SessionId}", sessionId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
