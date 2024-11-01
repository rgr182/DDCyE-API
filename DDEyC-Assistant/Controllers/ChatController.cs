using Microsoft.AspNetCore.Mvc;
using DDEyC_Assistant.Services.Interfaces;
using DDEyC_Assistant.Models.DTOs;
using DDEyC_Assistant.Attributes;
using DDEyC_Assistant.Exceptions;
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
        [ProducesResponseType(typeof(ChatStartResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StartChat()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var result = await _chatService.StartChatAsync(userId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chat");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred while starting the chat",
                    ErrorCode = "CHAT_START_ERROR"
                });
            }
        }

        [HttpPost("Chat")]
        [RequireAuth]
        [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Chat([FromBody] ChatRequestDto chatRequest)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var response = await _chatService.ProcessChatAsync(chatRequest, userId);
                return Ok(response);
            }
            catch (ChatServiceException ex) when (ex.ErrorCode == "INVALID_THREAD")
            {
                return BadRequest(new ErrorResponse
                {
                    Message = ex.Message,
                    ErrorCode = ex.ErrorCode
                });
            }
            catch (ChatServiceException ex) when (ex.ErrorCode == "CONVERSATION_BUSY")
            {
                return StatusCode(409, new ErrorResponse
                {
                    Message = ex.Message,
                    ErrorCode = ex.ErrorCode
                });
            }
            catch (OpenAIServiceException ex) when (ex.ErrorCode == "RATE_LIMIT")
            {
                return StatusCode(429, new ErrorResponse
                {
                    Message = "Service is currently rate limited. Please try again later.",
                    ErrorCode = "RATE_LIMIT"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An unexpected error occurred",
                    ErrorCode = "INTERNAL_ERROR"
                });
            }
        }

        [HttpGet("threads")]
        [RequireAuth]
        [ProducesResponseType(typeof(List<UserThreadDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllThreads()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var threads = await _chatService.GetAllThreadsForUser(userId);
                return Ok(threads);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user threads");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred while retrieving threads",
                    ErrorCode = "THREAD_RETRIEVAL_ERROR"
                });
            }
        }

        [HttpGet("threads/{threadId}")]
        [RequireAuth]
        [ProducesResponseType(typeof(UserThreadDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetThread(int threadId)
        {
            try
            {
                var thread = await _chatService.GetThreadById(threadId);
                if (thread == null)
                {
                    return NotFound(new ErrorResponse
                    {
                        Message = "Thread not found",
                        ErrorCode = "THREAD_NOT_FOUND"
                    });
                }
                return Ok(thread);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving thread");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred while retrieving the thread",
                    ErrorCode = "THREAD_RETRIEVAL_ERROR"
                });
            }
        }

        [HttpGet("threads/recent/{count}")]
        [RequireAuth]
        [ProducesResponseType(typeof(List<UserThreadDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRecentThreads(int count)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var threads = await _chatService.GetRecentThreadsForUser(userId, count);
                return Ok(threads);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent threads");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred while retrieving recent threads",
                    ErrorCode = "RECENT_THREADS_ERROR"
                });
            }
        }

        [HttpGet("threads/{threadId:int}/messages")]
        [RequireAuth]
        [ProducesResponseType(typeof(List<MessageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMessagesForThread(int threadId)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var messages = await _chatService.GetMessagesForThread(userId, threadId);
                return Ok(messages);
            }
            catch (ChatServiceException ex) when (ex.ErrorCode == "INVALID_THREAD_ACCESS")
            {
                return NotFound(new ErrorResponse
                {
                    Message = ex.Message,
                    ErrorCode = ex.ErrorCode
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages for thread");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred while retrieving messages",
                    ErrorCode = "MESSAGE_RETRIEVAL_ERROR"
                });
            }
        }

        [HttpPost("threads/{threadId}/favorite")]
        [RequireAuth]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ToggleThreadFavorite(int threadId, [FromBody] string note)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var isFavorite = await _chatService.ToggleThreadFavoriteAsync(userId, threadId, note);
                return Ok(new { isFavorite });
            }
            catch (ChatServiceException ex) when (ex.ErrorCode == "FAVORITE_TOGGLE_FAILED")
            {
                return BadRequest(new ErrorResponse
                {
                    Message = ex.Message,
                    ErrorCode = ex.ErrorCode
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling thread favorite");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred while toggling thread favorite",
                    ErrorCode = "FAVORITE_ERROR"
                });
            }
        }

        [HttpPost("messages/{messageId}/favorite")]
        [RequireAuth]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ToggleMessageFavorite(int messageId, [FromBody] string note)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var isFavorite = await _chatService.ToggleMessageFavoriteAsync(userId, messageId, note);
                return Ok(new { isFavorite });
            }
            catch (ChatServiceException ex) when (ex.ErrorCode == "FAVORITE_TOGGLE_FAILED")
            {
                return BadRequest(new ErrorResponse
                {
                    Message = ex.Message,
                    ErrorCode = ex.ErrorCode
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling message favorite");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred while toggling message favorite",
                    ErrorCode = "FAVORITE_ERROR"
                });
            }
        }

        [HttpGet("favorites/threads")]
        [RequireAuth]
        [ProducesResponseType(typeof(List<UserThreadDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFavoriteThreads()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var favorites = await _chatService.GetFavoriteThreadsAsync(userId);
                return Ok(favorites);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving favorite threads");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred while retrieving favorite threads",
                    ErrorCode = "FAVORITE_RETRIEVAL_ERROR"
                });
            }
        }

        [HttpGet("favorites/messages")]
        [RequireAuth]
        [ProducesResponseType(typeof(List<MessageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFavoriteMessages()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var favorites = await _chatService.GetFavoriteMessagesAsync(userId);
                return Ok(favorites);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse
                {
                    Message = "Invalid or expired token",
                    ErrorCode = "INVALID_TOKEN"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving favorite messages");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred while retrieving favorite messages",
                    ErrorCode = "FAVORITE_RETRIEVAL_ERROR"
                });
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
                    _logger.LogError("The token is not in a valid JWT format");
                    throw new InvalidOperationException("The token is not in a valid JWT format.");
                }

                var jsonToken = handler.ReadJwtToken(token);
                var idClaim = jsonToken.Claims.FirstOrDefault(claim =>
                    claim.Type == "Id" || claim.Type == "id" || claim.Type == "userId");

                if (idClaim == null || !int.TryParse(idClaim.Value, out int userId))
                {
                    _logger.LogError("Unable to extract user ID from token");
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