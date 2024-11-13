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
        private readonly Dictionary<string, int> _academicLevelIds;
        public JobListingService(IJobListingRepository repository, ILogger<JobListingService> logger, IConfiguration configuration)
        {
            _repository = repository;
            _logger = logger;
            (_academicPatterns, _academicLevelIds) = LoadAcademicPatterns(configuration);

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
                            var (levels, minimumLevel) = DetectAcademicLevels(doc.Description);

                            // Build update definition specifically for numeric values
                            var filter = Builders<JobListing>.Filter.Eq("id", doc.JobId);
                            var updateDefinition = new BsonDocument
                        {
                            {
                                "$set", new BsonDocument
                                {
                                    { "academic_levels", new BsonArray(levels.Select(l => new BsonInt32(l))) },
                                    { "minimum_academic_level", new BsonInt32(minimumLevel) }
                                }
                            }
                        };

                            bulkOps.Add(new UpdateOneModel<JobListing>(filter, updateDefinition));

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
        FilterDefinition<JobListing> CreateKeywordFilter(string fieldName, string value)
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
        private (List<int> levels, int minimumLevel) DetectAcademicLevels(string description)
        {
            if (string.IsNullOrEmpty(description))
                return (new List<int>(), 0);

            description = PreprocessDescription(description);
            var detectedLevels = new List<int>();

            foreach (var level in _academicPatterns)
            {
                if (level.Value.Any(pattern =>
                    Regex.IsMatch(description, pattern, RegexOptions.IgnoreCase)))
                {
                    var levelId = _academicLevelIds[level.Key];
                    detectedLevels.Add(levelId);
                    _logger.LogDebug("Found academic level {Level} (ID: {Id}) in description", level.Key, levelId);
                }
            }

            var minimumLevel = detectedLevels.Any() ? detectedLevels.Min() : 0;

            return (detectedLevels, minimumLevel);
        }

        private (Dictionary<string, string[]> patterns, Dictionary<string, int> levelIds) LoadAcademicPatterns(IConfiguration configuration)
        {
            try
            {
                var patternsPath = configuration["AcademicLevelPatterns:JsonPath"];
                if (string.IsNullOrEmpty(patternsPath))
                {
                    throw new InvalidOperationException("Patterns configuration path not set");
                }

                var jsonContent = File.ReadAllText(patternsPath);
                var patternsConfig = JsonSerializer.Deserialize<AcademicLevelPatternConfiguration>(
                    jsonContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (patternsConfig?.Patterns == null || !patternsConfig.Patterns.Any())
                {
                    throw new InvalidOperationException("No patterns found in configuration file");
                }

                var patterns = patternsConfig.Patterns.ToDictionary(p => p.Level, p => p.Patterns);
                var levelIds = new Dictionary<string, int>();

                // Assign IDs based on order in config (1-based)
                for (int i = 0; i < patternsConfig.Patterns.Count; i++)
                {
                    levelIds[patternsConfig.Patterns[i].Level] = i + 1;
                }

                return (patterns, levelIds);
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
        private string PreprocessDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return string.Empty;

            // Normalize spaces and line endings
            description = Regex.Replace(description, @"\s+", " ");

            // Normalize common variations
            description = description
                .Replace("b.s.", "bs")
                .Replace("m.s.", "ms")
                .Replace("ph.d.", "phd")
                .Replace("m.b.a.", "mba");

            return description.ToLowerInvariant().Trim();
        }
    }
}