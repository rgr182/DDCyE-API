using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using DDEyC_API.DataAccess.Repositories;
using MongoDB.Driver;
using MongoDB.Bson;

namespace DDEyC_API.DataAccess.Services
{
    public interface IJobListingService
    {
        Task<List<JobListing>> GetJobListingsAsync(JobListingFilter filter);
    }

    public class JobListingService : IJobListingService
    {
        private readonly IJobListingRepository _repository;
        private readonly ILogger<JobListingService> _logger;

        public JobListingService(IJobListingRepository repository, ILogger<JobListingService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<List<JobListing>> GetJobListingsAsync(JobListingFilter filter)
        {
            var builder = Builders<JobListing>.Filter;
            var filterDefinition = builder.Empty;

            if (!string.IsNullOrWhiteSpace(filter.Title))
            {
                filterDefinition &= builder.Regex(x => x.Title, new BsonRegularExpression(filter.Title, "i"));
            }

            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
            {
                filterDefinition &= builder.Regex(x => x.CompanyName, new BsonRegularExpression(filter.CompanyName, "i"));
            }

            if (!string.IsNullOrWhiteSpace(filter.Location))
            {
                filterDefinition &= builder.Regex(x => x.Location, new BsonRegularExpression(filter.Location, "i"));
            }

            if (!string.IsNullOrWhiteSpace(filter.Seniority))
            {
                filterDefinition &= builder.Regex(x => x.Seniority, new BsonRegularExpression($"^{Regex.Escape(filter.Seniority)}$", "i"));
            }

            if (!string.IsNullOrWhiteSpace(filter.EmploymentType))
            {
                filterDefinition &= builder.Regex(x => x.EmploymentType, new BsonRegularExpression($"^{Regex.Escape(filter.EmploymentType)}$", "i"));
            }

            if (filter.JobFunctions != null && filter.JobFunctions.Any())
            {
                filterDefinition &= builder.AnyIn(x => x.JobFunctions, filter.JobFunctions);
            }

            if (filter.Industries != null && filter.Industries.Any())
            {
                filterDefinition &= builder.ElemMatch(x => x.JobIndustries, y => filter.Industries.Contains(y.JobIndustryList.Industry));
            }

            int limit = filter.Limit > 0 ? filter.Limit : 10;

            var results = await _repository.GetJobListingsAsync(filterDefinition, limit);

            _logger.LogInformation($"Query returned {results.Count} results");

            return results;
        }
    }
}