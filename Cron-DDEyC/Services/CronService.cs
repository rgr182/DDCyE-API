using System;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Cron_DDEyC.Infraestructure;

namespace Cron_DDEyC.Services
{
    public class CronService
    {
        private readonly IMongoCollection<BsonDocument> _configurationCollection;

        public CronService(MongoDbConnection mongoDbConnection, string collectionName)
        {
            _configurationCollection = mongoDbConnection.GetCollection<BsonDocument>(collectionName);
        }

        public async Task<string> GetCronExpressionAsync()
        {
            // Adjust the filter to match your MongoDB schema
            var filter = Builders<BsonDocument>.Filter.Eq("_id", 1);
            var result = await _configurationCollection.Find(filter).FirstOrDefaultAsync();

            return result?["CronExpression"].AsString;
        }
    }
}
