// AgeResponseController.cs
using DDEyC_Assistant.Models.Entities;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class AgeResponseController : ControllerBase
{
    private readonly ILogger<AgeResponseController> _logger;

    public AgeResponseController(ILogger<AgeResponseController> logger)
    {
        _logger = logger;
    }

    [HttpGet("{age}")]
    public IActionResult GetAgeResponse(int age)
    {
        _logger.LogInformation($"Age: {age}");
        if (age < 18)
        {
            return Ok(new { message = "You're so young! Enjoy your youth and keep learning.", category = "Youth" });
        }
        else if (age >= 18 && age < 60)
        {
            return Ok(new { message = "Adulthood is an adventure. Make the most of every opportunity!", category = "Adult" });
        }
        else
        {
            return Ok(new { message = "Wisdom comes with age. Your experience is invaluable!", category = "Senior" });
        }
    }

    [HttpPost("SubmitData")]
    public IActionResult SubmitData([FromBody] FormData formData)
    {
        _logger.LogInformation($"Received form data: Name: {formData.fullName}, Email: {formData.email}, Age: {formData.age}");

        if (!int.TryParse(formData.age, out int age))
        {
            return BadRequest("Invalid age provided");
        }

        if (age < 18)
        {
            return Ok(new { message = "You're so young! Enjoy your youth and keep learning.", category = "Youth" });
        }
        else if (age >= 18 && age < 60)
        {
            return Ok(new { message = "Adulthood is an adventure. Make the most of every opportunity!", category = "Adult" });
        }
        else
        {
            return Ok(new { message = "Wisdom comes with age. Your experience is invaluable!", category = "Senior" });
        }
    }
}