using MongoDB.Driver;

namespace Cron_BolsaDeTrabajo.Infrastructure
{
    // Interface for MongoDB Connection
    public interface IMongoDbConnection
    {
        IMongoCollection<T> GetCollection<T>(string collectionName);
    }

    // Implementation of MongoDB Connection
    public class MongoDbConnection : IMongoDbConnection
    {
        private readonly IMongoClient _client;
        private readonly IMongoDatabase _database;

        public MongoDbConnection(string connectionString, string databaseName)
        {
            _client = new MongoClient(connectionString);
            _database = _client.GetDatabase(databaseName);
        }

        public IMongoCollection<T> GetCollection<T>(string collectionName)
        {
            return _database.GetCollection<T>(collectionName);
        }
    }
}
