using System.Text.RegularExpressions;
using DDEyC_API.DataAccess.Repositories;
using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
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
        private readonly IMemoryCache _cache;
        private readonly ITextNormalizationService _textNormalizer;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

        public CourseService(
            ICourseRepository repository,
            ILogger<CourseService> logger,
            IMemoryCache cache,
            ITextNormalizationService textNormalizer)
        {
            _repository = repository;
            _logger = logger;
            _cache = cache;
            _textNormalizer = textNormalizer;
        }

        public async Task<List<Course>> GetRecommendedCoursesAsync(CourseFilter filter)
        {
            var cacheKey = $"courses_{filter.SearchTerm}_{filter.Location}_{filter.Limit}";

            if (_cache.TryGetValue(cacheKey, out List<Course> cachedCourses))
            {
                _logger.LogInformation("Returning cached course results for key: {CacheKey}", cacheKey);
                return cachedCourses ?? new List<Course>();
            }

            var builder = Builders<Course>.Filter;
            var filters = new List<FilterDefinition<Course>>
            {
                builder.Eq(x => x.IsActive, true)
            };

            if (!string.IsNullOrWhiteSpace(filter.Location))
            {
                var locationFilter = builder.Regex(x => x.Location, new BsonRegularExpression(filter.Location, "i"));
                filters.Add(locationFilter);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerms = TokenizeSearchTerm(filter.SearchTerm);
                if (searchTerms.Any())
                {
                    var termFilters = searchTerms.Select(term =>
{
    // Always normalize the search term regardless of diacritics
    var normalizedTerm = _textNormalizer.RemoveAllDiacritics(term.ToLowerInvariant());

    return builder.Or(
        // Original field search
        builder.Regex(x => x.Title, new BsonRegularExpression($".*{Regex.Escape(term)}.*", "i")),
        builder.Regex(x => x.Description, new BsonRegularExpression($".*{Regex.Escape(term)}.*", "i")),
        // Normalized field search - always include this
        builder.Regex(x => x.NormalizedTitle, new BsonRegularExpression($".*{Regex.Escape(normalizedTerm)}.*", "i")),
        builder.Regex(x => x.NormalizedDescription, new BsonRegularExpression($".*{Regex.Escape(normalizedTerm)}.*", "i"))
    );
});

                    filters.Add(builder.Or(termFilters.ToArray()));
                }
            }

            var filterDefinition = builder.And(filters);

            var options = new FindOptions<Course>
            {
                Collation = new Collation("es", strength: CollationStrength.Secondary)
            };

            _logger.LogInformation("Executing course search with filter: {Filter}",
                filterDefinition.Render(BsonSerializer.SerializerRegistry.GetSerializer<Course>(),
                BsonSerializer.SerializerRegistry));

            var courses = await _repository.GetCoursesAsync(filterDefinition, filter.Limit, options);

            _cache.Set(cacheKey, courses, CacheDuration);

            _logger.LogInformation("Found {Count} courses matching criteria", courses.Count);

            return courses;
        }

        private IEnumerable<string> TokenizeSearchTerm(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<string>();

            return searchTerm.ToLowerInvariant()
                .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(term => term.Trim())
                .Where(term => !string.IsNullOrWhiteSpace(term));
        }
    }
}