using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        private const int MaxLevenshteinDistance = 2;

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
                filterDefinition &= CreateFlexibleTextFilter("Title", filter.Title);
            }

            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
            {
                filterDefinition &= CreateFlexibleTextFilter("CompanyName", filter.CompanyName);
            }

            if (!string.IsNullOrWhiteSpace(filter.Location))
            {
                filterDefinition &= CreateLocationFilter(filter.Location);
            }

            if (!string.IsNullOrWhiteSpace(filter.Seniority))
            {
                filterDefinition &= CreateFlexibleTextFilter("Seniority", filter.Seniority);
            }

            if (!string.IsNullOrWhiteSpace(filter.EmploymentType))
            {
                filterDefinition &= CreateFlexibleTextFilter("EmploymentType", filter.EmploymentType);
            }

            if (filter.JobFunctions != null && filter.JobFunctions.Any())
            {
                var jobFunctionFilters = filter.JobFunctions.Select(jf => 
                    builder.ElemMatch("JobFunctions", CreateFlexibleTextFilter("", jf))
                );
                filterDefinition &= builder.Or(jobFunctionFilters);
            }

            if (filter.Industries != null && filter.Industries.Any())
            {
                var industryFilters = filter.Industries.Select(industry =>
                    builder.ElemMatch("JobIndustries", 
                        CreateFlexibleTextFilter("JobIndustryList.Industry", industry)
                    )
                );
                filterDefinition &= builder.Or(industryFilters);
            }

            int limit = filter.Limit > 0 ? filter.Limit : 10;

            _logger.LogInformation($"Executing query with filter: {filterDefinition.Render(BsonSerializer.LookupSerializer(typeof(JobListing)) as IBsonSerializer<JobListing>, null)}");

            var results = await _repository.GetJobListingsAsync(filterDefinition, limit);

            _logger.LogInformation($"Query returned {results.Count} results");

            return results;
        }

        private FilterDefinition<JobListing> CreateFlexibleTextFilter(string fieldName, string value)
        {
            var builder = Builders<JobListing>.Filter;
            var words = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            var wordFilters = words.Select(word => 
            {
                var withoutDiacritics = RemoveDiacritics(word.ToLowerInvariant());
                var withPotentialDiacritics = AddPotentialDiacritics(withoutDiacritics);
                var fuzzyPattern = CreateFuzzyPattern(withoutDiacritics);
                
                return builder.Or(
                    builder.Regex(fieldName, new BsonRegularExpression($".*{Regex.Escape(withoutDiacritics)}.*", "i")),
                    builder.Regex(fieldName, new BsonRegularExpression($".*{withPotentialDiacritics}.*", "i")),
                    builder.Regex(fieldName, new BsonRegularExpression(fuzzyPattern, "i"))
                );
            });

            return builder.And(wordFilters);
        }

        private FilterDefinition<JobListing> CreateLocationFilter(string location)
        {
            return CreateFlexibleTextFilter("Location", location);
        }

        private static string RemoveDiacritics(string text)
        {
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

        private static string AddPotentialDiacritics(string input)
        {
            var result = new StringBuilder();
            foreach (char c in input)
            {
                switch (c)
                {
                    case 'a':
                        result.Append("[aáàâäã]");
                        break;
                    case 'e':
                        result.Append("[eéèêë]");
                        break;
                    case 'i':
                        result.Append("[iíìîï]");
                        break;
                    case 'o':
                        result.Append("[oóòôöõ]");
                        break;
                    case 'u':
                        result.Append("[uúùûü]");
                        break;
                    case 'n':
                        result.Append("[nñ]");
                        break;
                    default:
                        result.Append(c);
                        break;
                }
            }
            return result.ToString();
        }

        private static string CreateFuzzyPattern(string word)
        {
            if (word.Length <= MaxLevenshteinDistance)
            {
                return $".*{Regex.Escape(word)}.*";
            }

            var fuzzyPattern = new StringBuilder();
            fuzzyPattern.Append(".*");

            for (int i = 0; i < word.Length; i++)
            {
                fuzzyPattern.Append('(');
                for (int j = Math.Max(0, i - MaxLevenshteinDistance); j < Math.Min(word.Length, i + MaxLevenshteinDistance + 1); j++)
                {
                    if (j > Math.Max(0, i - MaxLevenshteinDistance))
                    {
                        fuzzyPattern.Append('|');
                    }
                    fuzzyPattern.Append(Regex.Escape(word[j].ToString()));
                }
                fuzzyPattern.Append(')');
            }

            fuzzyPattern.Append(".*");
            return fuzzyPattern.ToString();
        }
    }
}