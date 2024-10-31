using DDEyC_API.Models;
using MongoDB.Driver;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface ICourseRepository
    {
        Task<List<Course>> GetCoursesAsync(FilterDefinition<Course> filter, int limit);
        Task BulkInsertAsync(IEnumerable<Course> courses);
        Task DeleteAllAsync();
    }
    public class CourseRepository : ICourseRepository
    {
        private readonly IMongoCollection<Course> _courses;

        public CourseRepository(IConfiguration configuration)
        {
            var connectionString = configuration["MongoDbSettings:ConnectionString"];
            var databaseName = configuration["MongoDbSettings:DatabaseName"];
            var collectionName = "courses";

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _courses = database.GetCollection<Course>(collectionName);
        }

        public async Task<List<Course>> GetCoursesAsync(FilterDefinition<Course> filter, int limit)
        {
            return await _courses.Find(filter)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task BulkInsertAsync(IEnumerable<Course> courses)
        {
            if (courses.Any())
            {
                await _courses.InsertManyAsync(courses);
            }
        }

        public async Task DeleteAllAsync()
        {
            await _courses.DeleteManyAsync(Builders<Course>.Filter.Empty);
        }
    }
}