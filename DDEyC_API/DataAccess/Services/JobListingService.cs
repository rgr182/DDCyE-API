using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using DDEyC_API.DataAccess.Repositories;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Text.Json;

namespace DDEyC_API.DataAccess.Services
{
    public interface IJobListingService
    {
        Task<List<JobListing>> GetJobListingsAsync(JobListingFilter filter);
        Task<MigrationStats> AddAcademicLevelsAsync(int batchSize = 1000);
    }

    public class JobListingService : IJobListingService
    {
        private readonly IJobListingRepository _repository;
        private readonly ILogger<JobListingService> _logger;
        private readonly Dictionary<string, string[]> _academicPatterns;
        public JobListingService(IJobListingRepository repository, ILogger<JobListingService> logger, IConfiguration configuration)
        {
            _repository = repository;
            _logger = logger;
            _academicPatterns = LoadAcademicPatterns(configuration);

        }

        public async Task<List<JobListing>> GetJobListingsAsync(JobListingFilter filter)
        {
            var builder = Builders<JobListing>.Filter;
            var filterDefinition = builder.Empty;

            if (!string.IsNullOrWhiteSpace(filter.Title))
            {
                filterDefinition &= CreateKeywordFilter("Title", filter.Title);
            }

            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
            {
                filterDefinition &= CreateKeywordFilter("CompanyName", filter.CompanyName);
            }

            if (!string.IsNullOrWhiteSpace(filter.Location))
            {
                filterDefinition &= CreateKeywordFilter("Location", filter.Location);
            }

            if (!string.IsNullOrWhiteSpace(filter.Seniority))
            {
                filterDefinition &= CreateKeywordFilter("Seniority", filter.Seniority);
            }

            if (!string.IsNullOrWhiteSpace(filter.EmploymentType))
            {
                filterDefinition &= CreateKeywordFilter("EmploymentType", filter.EmploymentType);
            }

            if (filter.JobFunctions != null && filter.JobFunctions.Any())
            {
                var jobFunctionFilters = filter.JobFunctions
                    .Where(jf => !string.IsNullOrWhiteSpace(jf))
                    .Select(jf =>
                    {
                        var keywords = TokenizeInput(jf);
                        var keywordFilters = keywords.Select(keyword =>
                        {
                            var withoutDiacritics = RemoveDiacritics(keyword);
                            var fuzzyPattern = CreateFuzzyPattern(withoutDiacritics);
                            var pattern = $".*{fuzzyPattern}.*";

                            return builder.Regex("job_functions_collection", new BsonRegularExpression(pattern, "i"));
                        });

                        return builder.And(keywordFilters);
                    });

                if (jobFunctionFilters.Any())
                {
                    filterDefinition &= builder.Or(jobFunctionFilters);
                }
            }

            if (filter.Industries != null && filter.Industries.Any())
            {
                var industryFilters = filter.Industries
                    .Where(industry => !string.IsNullOrWhiteSpace(industry))
                    .Select(industry =>
                    {
                        var keywords = TokenizeInput(industry);
                        var keywordFilters = keywords.Select(keyword =>
                        {
                            var withoutDiacritics = RemoveDiacritics(keyword);
                            var fuzzyPattern = CreateFuzzyPattern(withoutDiacritics);
                            var pattern = $".*{fuzzyPattern}.*";

                            return builder.ElemMatch("job_industry_collection",
                                Builders<JobIndustry>.Filter.Regex(
                                    "job_industry_list.industry",
                                    new BsonRegularExpression(pattern, "i")
                                )
                            );
                        });

                        return builder.And(keywordFilters);
                    });

                if (industryFilters.Any())
                {
                    filterDefinition &= builder.Or(industryFilters);
                }
            }

            int limit = filter.Limit > 0 ? filter.Limit : 10;

            var results = await _repository.GetJobListingsAsync(filterDefinition, limit);

            _logger.LogInformation($"Query returned {results.Count} results");

            return results;
        }

        public async Task<MigrationStats> AddAcademicLevelsAsync(int batchSize = 1000)
        {
            var stats = new MigrationStats
            {
                TotalProcessed = 0,
                Updated = 0,
                NoChanges = 0,
                Errors = 0
            };

            try
            {
                _logger.LogInformation("Starting academic level detection process");

                var cursor = await _repository.GetAllJobListingsAsync();
                var bulkOps = new List<WriteModel<JobListing>>();

                while (await cursor.MoveNextAsync())
                {
                    foreach (var doc in cursor.Current)
                    {
                        try
                        {
                            var academicLevel = DetectAcademicLevel(doc.Description);

                            // Use JobId instead of Id for the filter
                            var filter = Builders<JobListing>.Filter.Eq("id", doc.JobId);

                            var update = Builders<JobListing>.Update
                                .Set("academic_level", academicLevel);

                            bulkOps.Add(new UpdateOneModel<JobListing>(filter, update));

                            if (bulkOps.Count >= batchSize)
                            {
                                var result = await _repository.BulkUpdateAsync(bulkOps);
                                UpdateStats(stats, bulkOps.Count, result.ModifiedCount);
                                bulkOps.Clear();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing document with JobId: {doc.JobId}");
                            stats.Errors++;
                        }
                    }
                }

                if (bulkOps.Any())
                {
                    var result = await _repository.BulkUpdateAsync(bulkOps);
                    UpdateStats(stats, bulkOps.Count, result.ModifiedCount);
                }

                LogResults(stats);
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Academic level detection process failed");
                throw;
            }
        }
        private FilterDefinition<JobListing> CreateKeywordFilter(string fieldName, string value)
        {
            var builder = Builders<JobListing>.Filter;
            var keywords = TokenizeInput(value);

            var keywordFilters = keywords.Select(keyword =>
            {
                var withoutDiacritics = RemoveDiacritics(keyword);
                var fuzzyPattern = CreateFuzzyPattern(withoutDiacritics);

                return builder.Or(
                    builder.Regex(fieldName, new BsonRegularExpression($"{Regex.Escape(keyword)}", "i")),
                    builder.Regex(fieldName, new BsonRegularExpression($"{Regex.Escape(withoutDiacritics)}", "i")),
                    builder.Regex(fieldName, new BsonRegularExpression(fuzzyPattern, "i"))
                );
            });

            return builder.Or(keywordFilters);
        }

        private IEnumerable<string> TokenizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Enumerable.Empty<string>();
            }

            return input.ToLowerInvariant()
                .Split(new[] { ' ', ',', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(keyword => keyword.Trim())
                .Where(keyword => keyword.Length > 1);
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string CreateFuzzyPattern(string word)
        {
            if (string.IsNullOrWhiteSpace(word) || word.Length <= 3)
            {
                return Regex.Escape(word ?? string.Empty);
            }

            var sb = new StringBuilder();

            for (int i = 0; i < word.Length; i++)
            {
                sb.Append('[');
                if (i > 0) sb.Append(Regex.Escape(word[i - 1].ToString()));
                sb.Append(Regex.Escape(word[i].ToString()));
                if (i < word.Length - 1) sb.Append(Regex.Escape(word[i + 1].ToString()));
                sb.Append("]");
            }

            return sb.ToString();
        }
        private string? DetectAcademicLevel(string description)
        {
            if (string.IsNullOrEmpty(description))
                return null;

            description = description.ToLowerInvariant();

            foreach (var level in _academicPatterns)
            {
                if (level.Value.Any(pattern =>
                    Regex.IsMatch(description, pattern, RegexOptions.IgnoreCase)))
                {
                    return level.Key;
                }
            }

            return null;
        }
        private Dictionary<string, string[]> LoadAcademicPatterns(IConfiguration configuration)
        {
            try
            {
                var patternsPath = configuration["AcademicLevelPatterns:JsonPath"];
                if (string.IsNullOrEmpty(patternsPath))
                {
                    throw new InvalidOperationException("Patterns configuration path not set");
                }

                var jsonContent = File.ReadAllText(patternsPath);
                var patterns = JsonSerializer.Deserialize<AcademicLevelPatternConfiguration>(
                    jsonContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                return patterns?.Patterns.ToDictionary(p => p.Level, p => p.Patterns)
                    ?? new Dictionary<string, string[]>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading academic patterns");
                throw;
            }
        }


        private void UpdateStats(MigrationStats stats, int processedCount, long modifiedCount)
        {
            stats.TotalProcessed += processedCount;
            stats.Updated += (int)modifiedCount;
            stats.NoChanges += processedCount - (int)modifiedCount;

            _logger.LogInformation($"Processed batch of {processedCount} documents");
        }

        private void LogResults(MigrationStats stats)
        {
            _logger.LogInformation(
                "Process completed:\n" +
                "Total documents processed: {TotalProcessed}\n" +
                "Documents updated: {Updated}\n" +
                "Documents unchanged: {NoChanges}\n" +
                "Errors: {Errors}",
                stats.TotalProcessed,
                stats.Updated,
                stats.NoChanges,
                stats.Errors);
        }
    }
}