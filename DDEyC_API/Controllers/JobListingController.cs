using Microsoft.AspNetCore.Mvc;
using DDEyC_API.Models;
using DDEyC_API.DataAccess.Services;
using DDEyC_API.Models.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace DDEyC_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobListingsController : ControllerBase
    {
        private readonly IJobListingService _jobListingService;
        private readonly ILogger<JobListingsController> _logger;

        public JobListingsController(IJobListingService jobListingService, ILogger<JobListingsController> logger)
        {

            _jobListingService = jobListingService;
            _logger = logger;
        }
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<List<JobListing>>> GetJobListings([FromQuery] JobListingFilter filter)
        {
            var jobListings = await _jobListingService.GetJobListingsAsync(filter);
            return Ok(jobListings);
        }
        [AllowAnonymous]
        [HttpPost("academic-levels")]
        public async Task<ActionResult<MigrationStats>> AddAcademicLevels([FromQuery] int batchSize = 1000)
        {
            try
            {
                var stats = await _jobListingService.AddAcademicLevelsAsync(batchSize);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during academic level detection");
                return StatusCode(500, "An error occurred during the process");
            }
        }
    }
}