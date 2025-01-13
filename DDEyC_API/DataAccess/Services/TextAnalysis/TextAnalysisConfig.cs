namespace DDEyC_API.Services.TextAnalysis
{
 public class TextAnalysisConfig
{
    public Dictionary<string, string> SeniorityPatterns { get; set; }
    public Dictionary<string, int> EducationPatterns { get; set; }
    public Dictionary<string, int> RequiredEducationPatterns { get; set; }
    public Dictionary<string, string> ExperienceToSeniority { get; set; }
    public Dictionary<string, int> ExperienceToEducation { get; set; }
}
}