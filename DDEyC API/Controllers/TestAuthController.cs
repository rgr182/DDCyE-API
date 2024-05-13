using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DDEyC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestAuthController : ControllerBase
    {
        [Authorize]
        [HttpGet("test")]
        public IActionResult Test()
        {
            try
            {
                // If authentication is successful, it will reach this point and return a success message.
                return Ok("Works!");
            }
            catch (Exception)
            {
                // If authentication fails, the exception is caught, and it returns a custom error message.
                return Unauthorized("Authentication failed. Please check your credentials.");
            }
        }
    }
}
