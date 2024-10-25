using Microsoft.AspNetCore.Mvc;
using DDEyC_API.Models;
using DDEyC_API.DataAccess.Services;
using DDEyC_API.Models.DTOs;

namespace DDEyC_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobListingsController : ControllerBase
    {
        private readonly IJobListingService _jobListingService;

        public JobListingsController(IJobListingService jobListingService)
        {
            
            _jobListingService = jobListingService;
        }

        [HttpGet]
        public async Task<ActionResult<List<JobListing>>> GetJobListings([FromQuery] JobListingFilter filter)
        {
            var jobListings = await _jobListingService.GetJobListingsAsync(filter);
            return Ok(jobListings);
        }
    }
}