using Microsoft.AspNetCore.Mvc;
using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using DDEyC_API.Services.JSearch;
using Microsoft.AspNetCore.Authorization;

namespace DDEyC_API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class JobListingController : ControllerBase
    {
        private readonly IJSearchService _jSearchService;
        private readonly ILogger<JobListingController> _logger;

        public JobListingController(
            IJSearchService jSearchService,
            ILogger<JobListingController> logger)
        {
            _jSearchService = jSearchService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves job listings based on the provided filter criteria
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<JobListing>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<List<JobListing>>> GetJobListings(
            [FromQuery] JobListingFilter filter,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation(
                    "Retrieving job listings with filter: {@Filter}",
                    filter);

                // Initialize filter if null
                filter ??= new JobListingFilter();

                // Apply default and max limits
                if (filter.Limit <= 0)
                {
                    filter.Limit = 10; // Default limit
                }
                else if (filter.Limit > 100)
                {
                    filter.Limit = 100; // Maximum limit
                }

                var jobListings = await _jSearchService.SearchJobListingsAsync(
                    filter,
                    cancellationToken);

                _logger.LogInformation(
                    "Successfully retrieved {Count} job listings",
                    jobListings.Count);

                return Ok(jobListings);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Request was cancelled");
                return StatusCode(
                    StatusCodes.Status499ClientClosedRequest,
                    new ProblemDetails
                    {
                        Title = "Request Cancelled",
                        Detail = "The request was cancelled by the client.",
                        Status = StatusCodes.Status499ClientClosedRequest
                    });
            }
            catch (JSearchException ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving job listings with filter: {@Filter}",
                    filter);

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new ProblemDetails
                    {
                        Title = "Service Unavailable",
                        Detail = "The job search service is temporarily unavailable. Please try again later.",
                        Status = StatusCodes.Status503ServiceUnavailable
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error retrieving job listings with filter: {@Filter}",
                    filter);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new ProblemDetails
                    {
                        Title = "Internal Server Error",
                        Detail = "An unexpected error occurred while processing your request.",
                        Status = StatusCodes.Status500InternalServerError
                    });
            }
        }

        /// <summary>
        /// Health check endpoint for the job listing service
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> HealthCheck(CancellationToken cancellationToken)
        {
            try
            {
                // Perform a lightweight search to verify service availability
                var testFilter = new JobListingFilter { Limit = 1 };
                await _jSearchService.SearchJobListingsAsync(testFilter, cancellationToken);

                return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new ProblemDetails
                    {
                        Title = "Service Unhealthy",
                        Detail = "The job search service is currently unavailable.",
                        Status = StatusCodes.Status503ServiceUnavailable
                    });
            }
        }
    }
}