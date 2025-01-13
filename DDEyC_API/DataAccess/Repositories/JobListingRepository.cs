using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface IJobListingRepository
    {
        Task<List<JobListing>> GetJobListingsAsync(FilterDefinition<JobListing> filter, int limit);
        Task<IAsyncCursor<JobListingUpdateDTO>> GetAllJobListingsAsync();
        Task<BulkWriteResult<JobListing>> BulkUpdateAsync(IEnumerable<WriteModel<JobListing>> updates);
    }

    public class JobListingRepository : IJobListingRepository
    {
        private readonly IMongoCollection<JobListing> _jobListings;
        private readonly IMongoDatabase _database;

        public JobListingRepository(IConfiguration configuration)
        {
            var connectionString = configuration["MongoDbSettings:ConnectionString"];
            var databaseName = configuration["MongoDbSettings:DatabaseName"];
            var collectionName = configuration["MongoDbSettings:CollectionName"];

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _database = database;
            _jobListings = database.GetCollection<JobListing>(collectionName);
        }

        public async Task<List<JobListing>> GetJobListingsAsync(FilterDefinition<JobListing> filter, int limit)
        {
            return await _jobListings.Find(filter).Limit(limit).ToListAsync();
        }
        public async Task<IAsyncCursor<JobListingUpdateDTO>> GetAllJobListingsAsync()
        {
            var projection = Builders<JobListing>.Projection
                .Exclude("_id")  // Explicitly exclude _id
                .Include("id")
                .Include("description");

            return await _jobListings
                .Find(Builders<JobListing>.Filter.Empty)
                .Project<JobListingUpdateDTO>(projection)
                .ToCursorAsync();
        }

        public async Task<BulkWriteResult<JobListing>> BulkUpdateAsync(IEnumerable<WriteModel<JobListing>> updates)
        {
            return await _jobListings.BulkWriteAsync(updates, new BulkWriteOptions { IsOrdered = false });
        }
    }
}