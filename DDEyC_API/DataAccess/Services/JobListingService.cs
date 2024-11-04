using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using DDEyC_API.DataAccess.Repositories;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace DDEyC_API.DataAccess.Services
{
    public interface IJobListingService
    {
        Task<List<JobListing>> GetJobListingsAsync(JobListingFilter filter);
    }

    public class JobListingService : IJobListingService
    {
        private readonly IJobListingRepository _repository;
        private readonly ILogger<JobListingService> _logger;

        public JobListingService(IJobListingRepository repository, ILogger<JobListingService> logger)
        {
            _repository = repository;
            _logger = logger;
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
    }
}