using Microsoft.AspNetCore.Mvc;
using DDEyC_Assistant.Services.Interfaces;
using DDEyC_Assistant.Models.DTOs;

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
        public async Task<IActionResult> StartChat()
        {
            try
            {
                var result = await _chatService.StartChatAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chat");
                return StatusCode(500, "An error occurred while starting the chat.");
            }
        }

        [HttpPost("Chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequestDto chatRequest)
        {
            try
            {
                var response = await _chatService.ProcessChatAsync(chatRequest);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat");
                return StatusCode(500, "An error occurred while processing the chat.");
            }
        }
    }
}