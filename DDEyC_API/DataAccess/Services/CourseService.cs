using System.Text.RegularExpressions;
using DDEyC_API.DataAccess.Repositories;
using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DDEyC_API.Services
{
    public interface ICourseService
    {
        Task<List<Course>> GetRecommendedCoursesAsync(CourseFilter filter);
    }

    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _repository;
        private readonly ILogger<CourseService> _logger;

        public CourseService(ICourseRepository repository, ILogger<CourseService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<List<Course>> GetRecommendedCoursesAsync(CourseFilter filter)
        {
            var builder = Builders<Course>.Filter;
            var filterDefinition = builder.Eq(x => x.IsActive, true);

            if (!string.IsNullOrWhiteSpace(filter.Location))
            {
                filterDefinition &= builder.Regex(x => x.Location, new BsonRegularExpression(filter.Location, "i"));
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchRegex = new BsonRegularExpression($".*{Regex.Escape(filter.SearchTerm)}.*", "i");
                filterDefinition &= builder.Or(
                    builder.Regex(x => x.Title, searchRegex),
                    builder.Regex(x => x.Description, searchRegex)
                );
            }

            return await _repository.GetCoursesAsync(filterDefinition, filter.Limit);
        }
    }
}

