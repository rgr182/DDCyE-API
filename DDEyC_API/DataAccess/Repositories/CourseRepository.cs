using DDEyC_API.Models;
using DDEyC_API.Services;
using MongoDB.Driver;

namespace DDEyC_API.DataAccess.Repositories
{
    public interface ICourseRepository
    {
        Task<List<Course>> GetCoursesAsync(FilterDefinition<Course> filter, int limit, FindOptions<Course> options = null);
        Task BulkInsertAsync(IEnumerable<Course> courses);
        Task DeleteAllAsync();
    }

    public class CourseRepository : ICourseRepository
    {
        private readonly IMongoCollection<Course> _courses;
        private readonly ILogger<CourseRepository> _logger;
        private readonly ITextNormalizationService _textNormalizer;

        public CourseRepository(
            IConfiguration configuration,
            ILogger<CourseRepository> logger,
            ITextNormalizationService textNormalizer)
        {
            _logger = logger;
            _textNormalizer = textNormalizer;
            var connectionString = configuration["MongoDbSettings:ConnectionString"];
            var databaseName = configuration["MongoDbSettings:DatabaseName"];
            var collectionName = "courses";

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _courses = database.GetCollection<Course>(collectionName);

            CreateIndexes();
        }

        private void CreateIndexes()
        {
            try
            {
                var indexModels = new CreateIndexModel<Course>[]
                {
                    new CreateIndexModel<Course>(
                        Builders<Course>.IndexKeys.Ascending(x => x.NormalizedTitle)),
                    new CreateIndexModel<Course>(
                        Builders<Course>.IndexKeys.Ascending(x => x.NormalizedDescription)),
                    new CreateIndexModel<Course>(
                        Builders<Course>.IndexKeys.Ascending(x => x.IsActive)),
                    new CreateIndexModel<Course>(
                        Builders<Course>.IndexKeys.Ascending(x => x.Date))
                };

                _courses.Indexes.CreateMany(indexModels);
                _logger.LogInformation("Successfully created indexes for courses collection");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating indexes for courses collection");
            }
        }

        public async Task<List<Course>> GetCoursesAsync(
            FilterDefinition<Course> filter,
            int limit,
            FindOptions<Course> options = null)
        {
            try
            {
                var findOptions = new FindOptions();
                if (options?.Collation != null)
                {
                    findOptions.Collation = options.Collation;
                }
                var query = _courses.Find(filter, findOptions);

                return await query.Limit(limit).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving courses with filter");
                throw;
            }
        }

        public async Task BulkInsertAsync(IEnumerable<Course> courses)
        {
            if (!courses?.Any() ?? true) return;

            try
            {
                foreach (var course in courses)
                {
                    course.NormalizedTitle = _textNormalizer.NormalizeTextSelectively(course.Title);
                    course.NormalizedDescription = _textNormalizer.NormalizeTextSelectively(course.Description);
                }

                await _courses.InsertManyAsync(courses);
                _logger.LogInformation("Successfully inserted {Count} courses", courses.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk insert of courses");
                throw;
            }
        }

        public async Task DeleteAllAsync()
        {
            try
            {
                await _courses.DeleteManyAsync(Builders<Course>.Filter.Empty);
                _logger.LogInformation("Successfully deleted all courses");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all courses");
                throw;
            }
        }
    }
}