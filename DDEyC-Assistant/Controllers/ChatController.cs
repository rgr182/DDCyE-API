// File: DDEyC_Assistant/Controllers/ChatController.cs

using Microsoft.AspNetCore.Mvc;
using DDEyC_Assistant.Services.Interfaces;
using DDEyC_Assistant.Models.DTOs;
using DDEyC_Assistant.Attributes;
using System.IdentityModel.Tokens.Jwt;


namespace DDEyC_Assistant.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpPost("StartChat")]
        [RequireAuth]
        public async Task<IActionResult> StartChat()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var result = await _chatService.StartChatAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chat");
                return StatusCode(500, "An error occurred while starting the chat.");
            }
        }

        [HttpPost("Chat")]
        [RequireAuth]
        public async Task<IActionResult> Chat([FromBody] ChatRequestDto chatRequest)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var response = await _chatService.ProcessChatAsync(chatRequest, userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat");
                return StatusCode(500, "An error occurred while processing the chat.");
            }
        }
        [HttpGet("threads")]
        [RequireAuth]
        public async Task<IActionResult> GetAllThreads()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var threads = await _chatService.GetAllThreadsForUser(userId);
                return Ok(threads);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user threads");
                return StatusCode(500, "An error occurred while retrieving user threads.");
            }
        }

        [HttpGet("threads/{threadId}")]
        [RequireAuth]
        public async Task<IActionResult> GetThread(int threadId)
        {
            try
            {
                var thread = await _chatService.GetThreadById(threadId);
                if (thread == null)
                {
                    return NotFound();
                }
                return Ok(thread);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving thread");
                return StatusCode(500, "An error occurred while retrieving the thread.");
            }
        }

        [HttpGet("threads/recent/{count}")]
        [RequireAuth]
        public async Task<IActionResult> GetRecentThreads(int count)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var threads = await _chatService.GetRecentThreadsForUser(userId, count);
                return Ok(threads);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent threads");
                return StatusCode(500, "An error occurred while retrieving recent threads.");
            }
        }
        [HttpGet("threads/{threadId:int}/messages")]
        [RequireAuth]
        public async Task<IActionResult> GetMessagesForThread(int threadId)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var messages = await _chatService.GetMessagesForThread(userId, threadId);
                return Ok(messages);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages for thread");
                return StatusCode(500, "An error occurred while retrieving messages for the thread.");
            }
        }
        private int GetUserIdFromToken()
        {
            try
            {
                var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader))
                {
                    throw new UnauthorizedAccessException("Invalid Authorization header");
                }

                var token = authHeader.Trim();

                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                {
                    _logger.LogError("The token is not in a valid JWT format. ${token}", token);
                    throw new InvalidOperationException("The token is not in a valid JWT format.");
                }

                var jsonToken = handler.ReadJwtToken(token);

                var idClaim = jsonToken.Claims.FirstOrDefault(claim => claim.Type == "Id" || claim.Type == "id" || claim.Type == "userId");
                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                {
                    throw new InvalidOperationException("Unable to extract user ID from token");
                }

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting user ID from token");
                throw new UnauthorizedAccessException("Invalid token", ex);
            }
        }
    }
}