using DDEyC_API.DataAccess.Models.DTOs;
using DDEyC_API.DataAccess.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata;

namespace DDEyC_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public EmailController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpPost]
        public async Task<IActionResult> SendEmail([FromBody] EmailRequestDTO request)
        {
            var result = await _emailService.SendEmailAsync(request);
            if (result)
                return Ok("Email sent successfully.");
            else
                return StatusCode(500, "Failed to send email.");
        }      
    }
}
