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

                // Filter to get documents containing the "id" field
                var filter = Builders<BsonDocument>.Filter.Exists("id");
                var projection = Builders<BsonDocument>.Projection.Include("id").Exclude("_id");

                // Retrieve documents that match the filter
                var result = await collection.Find(filter).Project(projection).ToListAsync();

                // Extract IDs from the retrieved documents using a switch expression
                foreach (var document in result)
                {
                    if (document.Contains("id"))
                    {
                        var idValue = document["id"];

                        var idString = idValue.BsonType switch
                        {
                            BsonType.Int64 => idValue.AsInt64.ToString(),
                            BsonType.Int32 => idValue.AsInt32.ToString(),
                            BsonType.String => idValue.AsString,
                            _ => throw new InvalidOperationException($"Unexpected ID type: {idValue.BsonType}")
                        };

                        jobIds.Add(idString);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while fetching job IDs: {ex.Message}");
            }

            return jobIds;
        }
    }
}
