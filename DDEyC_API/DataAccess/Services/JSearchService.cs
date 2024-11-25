using System.Text;
using System.Text.Json;
using System.Web;
using DDEyC_API.Infrastructure.Caching;
using DDEyC_API.Infrastructure.Http;
using DDEyC_API.Models;
using DDEyC_API.Models.DTOs;
using DDEyC_API.Models.JSearch;
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

        public JSearchService(
            ResilientHttpClient httpClient,
            ICacheService cacheService,
            ITextNormalizationService textNormalizationService,
            IOptions<JSearchOptions> options,
            ILogger<JSearchService> logger)
        {
            _httpClient = httpClient;
            _cacheService = cacheService;
            _textNormalizationService = textNormalizationService;
            _options = options.Value;
            _logger = logger;
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

            if (!string.IsNullOrWhiteSpace(filter.Title))
                searchTerms.Add(filter.Title);

            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
                searchTerms.Add($"company:{filter.CompanyName}");

            if (searchTerms.Any())
                queryParts.Add($"query={HttpUtility.UrlEncode(string.Join(" ", searchTerms))}");

            if (!string.IsNullOrWhiteSpace(filter.Location))
                queryParts.Add($"location={HttpUtility.UrlEncode(filter.Location)}");

            if (!string.IsNullOrWhiteSpace(filter.EmploymentType))
                queryParts.Add($"employment_type={filter.EmploymentType.ToUpperInvariant()}");

            queryParts.Add("page=1");
            queryParts.Add("num_pages=1");
            queryParts.Add($"date_posted={filter.DatePosted ?? "all"}");

            return $"?{string.Join("&", queryParts)}";
        }

        private JobListing MapToJobListing(JSearchJob job)
        {
            try
            {
                var location = new[]
                {
                    job.job_city,
                    job.job_state,
                    job.job_country
                }.Where(x => !string.IsNullOrEmpty(x));

                var jobIndustries = new List<JobIndustry>();
                if (!string.IsNullOrEmpty(job.job_naics_name))
                {
                    jobIndustries.Add(new JobIndustry
                    {
                        JobIndustryList = new JobIndustryList
                        {
                            Industry = job.job_naics_name
                        }
                    });
                }

                var academicLevels = new List<int>();
                var minimumAcademicLevel = 0;

                if (job.job_highlights?.Qualifications != null)
                {
                    foreach (var qual in job.job_highlights.Qualifications)
                    {
                        if (qual.Contains("Bachelor", StringComparison.OrdinalIgnoreCase))
                        {
                            academicLevels.Add(3);
                            minimumAcademicLevel = Math.Max(minimumAcademicLevel, 3);
                        }
                        if (qual.Contains("Master", StringComparison.OrdinalIgnoreCase) ||
                            qual.Contains("PhD", StringComparison.OrdinalIgnoreCase))
                        {
                            academicLevels.Add(4);
                            minimumAcademicLevel = Math.Max(minimumAcademicLevel, 4);
                        }
                        if (qual.Contains("Associate", StringComparison.OrdinalIgnoreCase))
                        {
                            academicLevels.Add(2);
                            minimumAcademicLevel = Math.Max(minimumAcademicLevel, 2);
                        }
                    }
                }

                return new JobListing
                {
                    // Id = ObjectId.GenerateNewId(),
                    JobId = GetHashCode(job.job_id),
                    Title = job.job_title,
                    Description = job.job_description,
                    EmploymentType = job.job_employment_type,
                    Location = string.Join(", ", location),
                    Country = job.job_country,
                    CompanyName = job.employer_name,
                    CompanyUrl = job.employer_website,
                    Url = job.job_apply_link,
                    Created = DateTime.TryParse(job.job_posted_at_datetime_utc,
                        out var created) ? created : DateTime.UtcNow,
                    LastUpdated = DateTime.TryParse(job.job_posted_at_datetime_utc,
                        out var updated) ? updated : DateTime.UtcNow,
                    TimePosted = "N/A",
                    Seniority = ExtractSeniority(job.job_title, job.job_highlights?.Qualifications),
                    Hash = job.job_id,
                    ExternalUrl = job.job_google_link,
                    Deleted = 0,
                    ApplicationActive = 1,
                    Salary = ExtractSalary(job.job_highlights?.Benefits),
                    RedirectedUrl = job.job_apply_link,
                    JobFunctions = job.job_highlights?.Responsibilities?.ToList() ?? new List<string>(),
                    JobIndustries = jobIndustries,
                    AcademicLevels = academicLevels.Distinct().ToList(),
                    MinimumAcademicLevel = minimumAcademicLevel
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping job {JobId}: {Error}", job.job_id, ex.Message);
                return null;
            }
        }

        private string ExtractSeniority(string jobTitle, List<string> qualifications)
        {
            var title = jobTitle?.ToLower() ?? "";
            var quals = qualifications ?? new List<string>();

            if (title.Contains("senior") || title.Contains("sr.") ||
                quals.Any(q => q.ToLower().Contains("senior")))
                return "Senior";

            if (title.Contains("junior") || title.Contains("jr.") ||
                title.Contains("entry") || title.Contains("intern"))
                return "Entry Level";

            if (title.Contains("lead") || title.Contains("manager") ||
                title.Contains("director"))
                return "Executive";

            return "Mid-Senior";
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
                filter.Title,
                filter.CompanyName,
                filter.Location,
                filter.EmploymentType,
                filter.DatePosted,
                filter.Limit
            });

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedFilter));
            return $"jsearch:jobs:{Convert.ToBase64String(hash)}";
        }

        private int GetHashCode(string input)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
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