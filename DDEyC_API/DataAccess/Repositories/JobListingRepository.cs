using DDEyC_API.Models;
using MongoDB.Driver;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface IJobListingRepository
    {
        Task<List<JobListing>> GetJobListingsAsync(FilterDefinition<JobListing> filter, int limit);
    }

    public class JobListingRepository : IJobListingRepository
    {
        private readonly IMongoCollection<JobListing> _jobListings;

        public JobListingRepository(IConfiguration configuration)
        {
            var connectionString = configuration["MongoDbSettings:ConnectionString"];
            var databaseName = configuration["MongoDbSettings:DatabaseName"];
            var collectionName = configuration["MongoDbSettings:CollectionName"];

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _jobListings = database.GetCollection<JobListing>(collectionName);
        }

        public async Task<List<JobListing>> GetJobListingsAsync(FilterDefinition<JobListing> filter, int limit)
        {
            return await _jobListings.Find(filter).Limit(limit).ToListAsync();
        }
    }
}