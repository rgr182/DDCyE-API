using Cron_BolsaDeTrabajo.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Cron_BolsaDeTrabajo.Services
{
    public interface ILinkedInJobService
    {
        Task<List<string>> GetLinkedInJobIdsFromDBAsync();
    }

    public class LinkedInJobService : ILinkedInJobService
    {
        private readonly IMongoDbConnection _mongoDbConnection;

        public LinkedInJobService(IMongoDbConnection mongoDbConnection)
        {
            _mongoDbConnection = mongoDbConnection;
        }

        public async Task<List<string>> GetLinkedInJobIdsFromDBAsync()
        {
            var jobIds = new List<string>();

            try
            {
                var collection = _mongoDbConnection.GetCollection<BsonDocument>("OfertasLaborales");

                // Filter to get documents containing the "linkedin_job_id" field
                var filter = Builders<BsonDocument>.Filter.Exists("linkedin_job_id");
                var projection = Builders<BsonDocument>.Projection.Include("linkedin_job_id").Exclude("_id");

                // Retrieve documents that match the filter
                var result = await collection.Find(filter).Project(projection).ToListAsync();

                // Extract IDs from the retrieved documents
                foreach (var document in result)
                {
                    if (document.Contains("linkedin_job_id"))
                    {
                        jobIds.Add(document["linkedin_job_id"].AsString);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while fetching LinkedIn job IDs: {ex.Message}");
            }

            return jobIds;
        }
    }
}
