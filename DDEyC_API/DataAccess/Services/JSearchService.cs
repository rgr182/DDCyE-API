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
                var searchResult = new JSearchResponse();
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;
                if (root.TryGetProperty("data", out var dataProperty) && dataProperty.ValueKind == JsonValueKind.Array)
                {
                    searchResult.Data = dataProperty.EnumerateArray()
                        .Select(property =>
                        {
                            var jobElement = property;
                            var job = new JSearchJob
                            {
                                job_id = jobElement.GetProperty("job_id").GetString(),
                                job_title = jobElement.GetProperty("job_title").GetString(),
                                employer_name = jobElement.GetProperty("employer_name").GetString(),
                                job_description = jobElement.GetProperty("job_description").GetString(),
                                job_city = jobElement.GetProperty("job_city").GetString(),
                                job_country = jobElement.GetProperty("job_country").GetString(),
                                job_employment_type = jobElement.GetProperty("job_employment_type").GetString(),
                                job_apply_link = jobElement.GetProperty("job_apply_link").GetString(),
                                employer_website = jobElement.GetProperty("employer_website").GetString(),
                                job_posted_at_datetime_utc = jobElement.GetProperty("job_posted_at_datetime_utc").GetString(),
                                // job_min_salary = jobElement.TryGetProperty("job_min_salary", out var minSalary) ? minSalary.GetDecimal() : (decimal?)null,
                                // job_max_salary = jobElement.TryGetProperty("job_max_salary", out var maxSalary) ? maxSalary.GetDecimal() : (decimal?)null,
                                job_salary_currency = jobElement.GetProperty("job_salary_currency").GetString(),
                                job_naics_name = jobElement.GetProperty("job_naics_name").GetString(),
                                job_highlights = JsonSerializer.Deserialize<JSearchJobHighlights>(jobElement.GetProperty("job_highlights").GetRawText()),
                                job_required_education = JsonSerializer.Deserialize<JSearchJobEducation>(jobElement.GetProperty("job_required_education").GetRawText())
                            };
                            return job;
                        })
                        .ToList();
                }

                if (searchResult?.Data == null || !searchResult.Data.Any())
                {
                    _logger.LogInformation("No results found for filter: {@Filter}", filter);
                    return new List<JobListing>();
                }

                var jobListings = searchResult.Data
                    .Take(filter.Limit)
                    .Select(MapToJobListing)
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

            // Build search terms
            if (!string.IsNullOrWhiteSpace(filter.Title))
                searchTerms.Add(filter.Title);

            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
                searchTerms.Add($"company:{filter.CompanyName}");

            if (searchTerms.Any())
                queryParts.Add($"query={HttpUtility.UrlEncode(string.Join(" ", searchTerms))}");

            // Location
            if (!string.IsNullOrWhiteSpace(filter.Location))
                queryParts.Add($"location={HttpUtility.UrlEncode(filter.Location)}");

            // Employment type
            if (!string.IsNullOrWhiteSpace(filter.EmploymentType))
                queryParts.Add($"employment_type={filter.EmploymentType.ToUpperInvariant()}");

            // Standard parameters
            queryParts.Add("page=1");
            queryParts.Add("num_pages=1");
            queryParts.Add($"date_posted={filter.DatePosted ?? "all"}");

            return $"?{string.Join("&", queryParts)}";
        }

        private JobListing MapToJobListing(JSearchJob job)
        {
            var skills = new HashSet<string>();
            var jobFunctions = new List<string>();

            // Extract skills and job functions from highlights if available
            if (job.job_highlights?.Qualifications != null)
            {
                foreach (var qualification in job.job_highlights.Qualifications)
                {
                    // Basic skill extraction - could be enhanced with NLP
                    var words = qualification.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var word in words)
                    {
                        if (IsTechnicalTerm(word))
                            skills.Add(word.Trim());
                    }
                }
            }

            if (job.job_highlights?.Responsibilities != null)
            {
                jobFunctions.AddRange(job.job_highlights.Responsibilities);
            }

            return new JobListing
            {
                JobId = int.TryParse(job.job_id, out int id) ? id : GetHashCode(job.job_id),
                Title = job.job_title,
                CompanyName = job.employer_name,
                Description = job.job_description,
                Location = job.job_city,
                Country = job.job_country,
                EmploymentType = job.job_employment_type,
                Url = job.job_apply_link,
                CompanyUrl = job.employer_website,
                Created = job.job_posted_at_datetime_utc != null 
                    ? DateTime.Parse(job.job_posted_at_datetime_utc) 
                    : DateTime.UtcNow,
                LastUpdated = job.job_posted_at_datetime_utc != null 
                    ? DateTime.Parse(job.job_posted_at_datetime_utc) 
                    : DateTime.UtcNow,
                JobFunctions = jobFunctions,
                JobIndustries = MapJobIndustries(job),
                Salary = FormatSalary(job),
                 // JSearch doesn't provide this info
                RedirectedUrl = job.job_google_link,
                AcademicLevels = MapAcademicLevels(job.job_required_education),
                MinimumAcademicLevel = GetMinimumAcademicLevel(job.job_required_education)
            };
        }

        private List<JobIndustry> MapJobIndustries(JSearchJob job)
        {
            var industries = new List<JobIndustry>();
            
            if (!string.IsNullOrEmpty(job.job_naics_name))
            {
                industries.Add(new JobIndustry
                {
                    JobIndustryList = new JobIndustryList
                    {
                        Industry = job.job_naics_name
                    }
                });
            }

            return industries;
        }

        private string FormatSalary(JSearchJob job)
        {
            if (job.job_min_salary == null && job.job_max_salary == null)
                return string.Empty;

            var currency = job.job_salary_currency ?? "USD";
            
            if (job.job_min_salary.HasValue && job.job_max_salary.HasValue)
                return $"{currency} {job.job_min_salary:N0} - {job.job_max_salary:N0}";
            
            if (job.job_min_salary.HasValue)
                return $"{currency} {job.job_min_salary:N0}+";
            
            if (job.job_max_salary.HasValue)
                return $"Up to {currency} {job.job_max_salary:N0}";

            return string.Empty;
        }

        private List<int> MapAcademicLevels(JSearchJobEducation? education)
        {
            var levels = new List<int>();

            if (education == null)
                return levels;

            // Map education levels to your existing academic level system
            if (education.high_school)
                levels.Add(1);
            if (education.associates_degree)
                levels.Add(2);
            if (education.bachelors_degree)
                levels.Add(3);
            if (education.postgraduate_degree)
                levels.Add(4);

            return levels;
        }

        private int GetMinimumAcademicLevel(JSearchJobEducation? education)
        {
            if (education == null)
                return 0;

            if (education.postgraduate_degree)
                return 4;
            if (education.bachelors_degree)
                return 3;
            if (education.associates_degree)
                return 2;
            if (education.high_school)
                return 1;

            return 0;
        }

        private bool IsTechnicalTerm(string word)
        {
            // This could be enhanced with a proper technical terms dictionary
            var technicalTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "javascript", "nodejs", "node.js", "react", "aws", "sql", "nosql",
                "mongodb", "express", "git", "api", "rest", "graphql"
            };

            return technicalTerms.Contains(word.Trim());
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
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalizedFilter));
            return $"jsearch:jobs:{Convert.ToBase64String(hash)}";
        }

        private int GetHashCode(string input)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Math.Abs(BitConverter.ToInt32(hash, 0));
        }
    }

    // Response Models
    public class JSearchResponse
    {
        public string status { get; set; }
        public string request_id { get; set; }
        public JSearchParameters parameters { get; set; }
        public List<JSearchJob> Data { get; set; }
    }

    public class JSearchParameters
    {
        public string query { get; set; }
        public int page { get; set; }
        public int num_pages { get; set; }
        public string date_posted { get; set; }
    }

    public class JSearchJob
    {
        public string job_id { get; set; }
        public string employer_name { get; set; }
        public string employer_website { get; set; }
        public string job_employment_type { get; set; }
        public string job_title { get; set; }
        public string job_apply_link { get; set; }
        public string job_description { get; set; }
        public bool job_is_remote { get; set; }
        public string job_posted_at_datetime_utc { get; set; }
        public string job_city { get; set; }
        public string job_state { get; set; }
        public string job_country { get; set; }
        public decimal? job_min_salary { get; set; }
        public decimal? job_max_salary { get; set; }
        public string job_salary_currency { get; set; }
        public string job_salary_period { get; set; }
        public string job_google_link { get; set; }
        public JSearchJobHighlights job_highlights { get; set; }
        public JSearchJobEducation job_required_education { get; set; }
        public string job_naics_code { get; set; }
        public string job_naics_name { get; set; }
    }

    public class JSearchJobHighlights
    {
        public List<string> Qualifications { get; set; }
        public List<string> Responsibilities { get; set; }
        public List<string> Benefits { get; set; }
    }

    public class JSearchJobEducation
    {
        public bool postgraduate_degree { get; set; }
        public bool professional_certification { get; set; }
        public bool high_school { get; set; }
        public bool associates_degree { get; set; }
        public bool bachelors_degree { get; set; }
    }

    public class JSearchException : Exception
    {
        public JSearchException(string message, Exception innerException = null) 
            : base(message, innerException)
        {
        }
    }
}