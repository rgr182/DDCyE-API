using Cron_BolsaDeTrabajo.Infrastructure;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Cron_BolsaDeTrabajo.Services
{
    public interface ILinkedInJobService
    {
        Task<List<string>> GetLinkedInJobIdsFromDBAsync();
        Task<List<string>> GetLinkedInJobUrlsFromDBAsync();
    }

    public class LinkedInJobService : ILinkedInJobService
    {
        private readonly IMongoDbConnection _mongoDbConnection;
        string _collectionName;

        public LinkedInJobService(IMongoDbConnection mongoDbConnection, IConfiguration configuration)
        {
            _collectionName = configuration["MongoDB:CollectionName"];
            _mongoDbConnection = mongoDbConnection;
        }

        public async Task<List<string>> GetLinkedInJobIdsFromDBAsync()
        {
            var jobIds = new List<string>();

            try
            {
                var collection = _mongoDbConnection.GetCollection<BsonDocument>(_collectionName);

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

        public async Task<List<string>> GetLinkedInJobUrlsFromDBAsync()
        {
            var jobUrls = new List<string>();

            try
            {
                var collection = _mongoDbConnection.GetCollection<BsonDocument>(_collectionName);

                // Filtro para obtener documentos que contengan el campo "url"
                var filter = Builders<BsonDocument>.Filter.Exists("url");
                var projection = Builders<BsonDocument>.Projection.Include("url").Exclude("_id");

                // Recuperar documentos que coincidan con el filtro
                var result = await collection.Find(filter).Project(projection).ToListAsync();

                // Extraer las URLs de los documentos recuperados
                foreach (var document in result)
                {
                    if (document.Contains("url"))
                    {
                        var urlValue = document["url"];

                        var urlString = urlValue.BsonType switch
                        {
                            BsonType.String => urlValue.AsString,
                            _ => throw new InvalidOperationException($"Unexpected URL type: {urlValue.BsonType}")
                        };

                        jobUrls.Add(urlString);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while fetching job URLs: {ex.Message}");
            }

            return jobUrls;
        }

    }
}
