using System.Text;
using System.Text.Json;
using System.Web;
using DDEyC_API.Infrastructure.Caching;
using DDEyC_API.Infrastructure.Http;
using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using DDEyC_API.Models.JSearch;
using DDEyC_API.Services.TextAnalysis;
using Microsoft.Extensions.Options;

namespace DDEyC_API.Services.JSearch
{
    public interface IJSearchService
    {
        Task<List<JobListing>> SearchJobListingsAsync(JobListingFilter filter, CancellationToken cancellationToken = default);
    }

    public class JSearchService : IJSearchService
    {
        private readonly ResilientHttpClient _httpClient;
        private readonly ICacheService _cacheService;
        private readonly ITextNormalizationService _textNormalizationService;
        private readonly JSearchOptions _options;
        private readonly ILogger<JSearchService> _logger;
        private readonly TextAnalysisExtractor _textAnalyzer;
        public JSearchService(
            ResilientHttpClient httpClient,
            ICacheService cacheService,
            ITextNormalizationService textNormalizationService,
            IOptions<JSearchOptions> options,
            IOptions<TextAnalysisConfig> textAnalysisConfig,
            ILogger<JSearchService> logger)
        {
            _httpClient = httpClient;
            _cacheService = cacheService;
            _textNormalizationService = textNormalizationService;
            _options = options.Value;
            _logger = logger;
            
            if (textAnalysisConfig.Value == null || textAnalysisConfig.Value.SeniorityPatterns == null)
            {
                _logger.LogError("TextAnalysis configuration is missing or invalid");
                throw new InvalidOperationException("TextAnalysis configuration is required");
            }
            _textAnalyzer = new TextAnalysisExtractor(textAnalysisConfig.Value, logger);
        }

        public async Task<List<JobListing>> SearchJobListingsAsync(
            JobListingFilter filter,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var cacheKey = GenerateCacheKey(filter);
                var cachedResult = await _cacheService.GetAsync<List<JobListing>>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogInformation("Cache hit for filter: {@Filter}", filter);
                    return cachedResult;
                }

                var url = $"{_options.BaseUrl}/search{BuildQueryString(filter)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                // _logger.LogDebug("Raw API Response: {Content}", content);

                var searchResult = JsonSerializer.Deserialize<JSearchResponse>(content);

                if (searchResult?.data == null || !searchResult.data.Any())
                {
                    _logger.LogInformation("No results found for filter: {@Filter}", filter);
                    return new List<JobListing>();
                }

                var jobListings = searchResult.data
                    .Take(filter.Limit)
                    .Select(MapToJobListing)
                    .Where(job => job != null)
                    .ToList();

                await _cacheService.SetAsync(
                    cacheKey,
                    jobListings,
                    TimeSpan.FromMinutes(_options.CacheExpirationMinutes));

                return jobListings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching job listings with filter: {@Filter}", filter);
                throw new JSearchException("Failed to search job listings", ex);
            }
        }

        private string BuildQueryString(JobListingFilter filter)
        {
            var queryParts = new List<string>();
            var searchTerms = new List<string>();

            if (!string.IsNullOrWhiteSpace(filter.Query))
                searchTerms.Add(filter.Query);

        _logger.LogInformation("Search term: {SearchTerm}", filter.Query);
        _logger.LogInformation("Search terms: {SearchTerms}", searchTerms.Count);
            if (searchTerms.Count != 0)
                queryParts.Add($"query={HttpUtility.UrlEncode(string.Join(" ", searchTerms))}");

            if (!string.IsNullOrWhiteSpace(filter.CountryCode))
                queryParts.Add($"country={filter.CountryCode.ToUpperInvariant()}");
            if (!string.IsNullOrWhiteSpace(filter.EmploymentType))
                queryParts.Add($"employment_type={filter.EmploymentType.ToUpperInvariant()}");

            if (filter.Remote.HasValue)
                queryParts.Add($"work_from_home={filter.Remote.Value.ToString().ToLower()}");
            if (filter.Page > 0 && filter.Limit > 0)
            {
                queryParts.Add($"page={filter.Page}");
            }
            else
            {
                queryParts.Add($"page=1");
            }
            queryParts.Add("num_pages=1");
            var excludedPublishers = _options.ExcludedJobPublishers ?? new List<string>();
            foreach (var excludedPublisher in excludedPublishers){
                _logger.LogInformation("Excluded job publisher: {ExcludedJobPublisher}", excludedPublisher);
            }
            if (excludedPublishers.Any())
            {
                queryParts.Add($"exclude_job_publishers={string.Join(",", excludedPublishers)}");
            }
            _logger.LogInformation("Excluded job publishers: {ExcludedJobPublishers}", excludedPublishers.Count);
            queryParts.Add($"date_posted={filter.DatePosted ?? "all"}");

            return $"?{string.Join("&", queryParts)}";
        }

        private JobListing MapToJobListing(JSearchJob job)
        {
            try
            {
                var location = new[] { job.job_city, job.job_state, job.job_country }
                    .Where(x => !string.IsNullOrEmpty(x));

       

                var (academicLevels, minimumLevel) = _textAnalyzer.ExtractJobMetadata(
                    job.job_description,
                    job.job_title,
                    job.job_highlights?.Qualifications
                );

                return new JobListing
                {
                    JobId = GetHashCode(job.job_id),
                    Title = job.job_title,
                    Description = job.job_description,
                    EmploymentType = job.job_employment_type,
                    Location = string.Join(", ", location),
                    Country = job.job_country,
                    CompanyName = job.employer_name,
                    Url = job.job_apply_link,
                    Created = DateTime.TryParse(job.job_posted_at_datetime_utc, out var created) ? created : DateTime.UtcNow,
                    Salary = ExtractSalary(job.job_highlights?.Benefits),
                    JobFunctions = job.job_highlights?.Responsibilities?.ToList() ?? new List<string>(),
                    AcademicLevels = academicLevels,
                    MinimumAcademicLevel = minimumLevel
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping job {JobId}: {Error}", job.job_id, ex.Message);
                return null;
            }
        }

        private string ExtractSalary(List<string> benefits)
        {
            if (benefits == null) return null;

            var salaryInfo = benefits.FirstOrDefault(b =>
                b.Contains("Pay Range") || b.Contains("Salary"));

            return salaryInfo;
        }

        private string GenerateCacheKey(JobListingFilter filter)
        {
            var normalizedFilter = JsonSerializer.Serialize(new
            {
                filter.Query,
                filter.Remote,             
                filter.EmploymentType,
                filter.DatePosted,
                filter.Page,
                filter.Limit
            });
            var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(normalizedFilter));
            return $"jsearch:jobs:{Convert.ToBase64String(hash)}";
        }

        private int GetHashCode(string input)
        {
            var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(input));
            return Math.Abs(BitConverter.ToInt32(hash, 0));
        }
    }

    public class JSearchException : Exception
    {
        public JSearchException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}