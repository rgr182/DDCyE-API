using System.Text.RegularExpressions;

namespace DDEyC_API.Services.TextAnalysis{
    public class TextAnalysisExtractor
{
    private readonly TextAnalysisConfig _config;
    private readonly ILogger _logger;

    public TextAnalysisExtractor(TextAnalysisConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public ( List<int> academicLevels, int minimumLevel) ExtractJobMetadata(
        string description, 
        string jobTitle, 
        List<string> qualifications)
    {
        var text = string.Join(" ", new[] { description, jobTitle, string.Join(" ", qualifications ?? new List<string>()) }
            ).ToLower();

        // var seniority = ExtractSeniority(text);
        var academicLevels = ExtractAcademicLevels(text);
        var minimumLevel = DetermineMinimumLevel(text);

        return (academicLevels, minimumLevel);
    }

    private List<int> ExtractAcademicLevels(string text)
    {
        var levels = new HashSet<int>();

        foreach (var pattern in _config.EducationPatterns)
        {
            if (Regex.IsMatch(text, pattern.Key, RegexOptions.IgnoreCase))
                levels.Add(pattern.Value);
        }

        foreach (var pattern in _config.ExperienceToEducation)
        {
            if (Regex.IsMatch(text, pattern.Key, RegexOptions.IgnoreCase))
                levels.Add(pattern.Value);
        }

        return levels.OrderBy(x => x).ToList();
    }

    private int DetermineMinimumLevel(string text)
    {
        foreach (var pattern in _config.RequiredEducationPatterns)
        {
            if (Regex.IsMatch(text, pattern.Key, RegexOptions.IgnoreCase))
                return pattern.Value;
        }

        var academicLevels = ExtractAcademicLevels(text);
        return academicLevels.Count != 0 ? academicLevels.Max() : 0;
    }
}
}