using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DDEyC_API.DataAccess.Services
{
    public interface IJobListingService
    {
        Task<List<JobListing>> GetJobListingsAsync(JobListingFilter filter);
    }
 public class JobListingService : IJobListingService
{
    private readonly IMongoCollection<JobListing> _jobListings;
    private readonly ILogger<JobListingService> _logger;
    public JobListingService(IConfiguration config, ILogger<JobListingService> logger)
    {
        var client = new MongoClient(config.GetSection("MongoDbSettings:ConnectionString").Value);
        var database = client.GetDatabase(config.GetSection("MongoDbSettings:DatabaseName").Value);
        _jobListings = database.GetCollection<JobListing>(config.GetSection("MongoDbSettings:CollectionName").Value);
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

        var query = _jobListings.Find(filterDefinition);

        int limit = filter.Limit > 0 ? filter.Limit : 10;

        _logger.LogInformation($"Executing query with filter: {filterDefinition.Render(_jobListings.DocumentSerializer, _jobListings.Settings.SerializerRegistry)}");

        var results = await query.Limit(limit).ToListAsync();

        _logger.LogInformation($"Query returned {results.Count} results");

        return results;
    }
}
}