using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using DDEyC_API.Services;
using Microsoft.AspNetCore.Mvc;
namespace DDEyC_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly ICourseService _courseService;
        private readonly ICourseImportService _importService;
        private readonly ILogger<CoursesController> _logger;

        public CoursesController(
            ICourseService courseService,
            ICourseImportService importService,
            ILogger<CoursesController> logger)
        {
            _courseService = courseService;
            _importService = importService;
            _logger = logger;
        }

        [HttpPost("recommendations")]
        [ProducesResponseType(typeof(List<Course>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<Course>>> GetRecommendations([FromBody] CourseFilter filter)
        {
            try
            {
                var courses = await _courseService.GetRecommendedCoursesAsync(filter);
                return Ok(courses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting course recommendations");
                return StatusCode(500, "Error retrieving course recommendations");
            }
        }

        [HttpPost("import")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImportCourses(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded");
                }

                if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("File must be an Excel document (.xlsx)");
                }

                await _importService.ImportFromExcel(file);
                return Ok("Courses imported successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing courses");
                return StatusCode(500, "Error importing courses");
            }
        }
    }
}