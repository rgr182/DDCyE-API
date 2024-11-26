namespace DDEyC_API.Models.DTOs
{
   public class JobListingFilter
{
    public string? Query { get; set; }
    public string? CountryCode { get; set; } = "MX";
    /// <summary>
    /// Find jobs of particular employment types, specified as a comma delimited list of the following values: 
    /// FULLTIME, CONTRACTOR, PARTTIME, INTERN.
    /// </summary>
    public string? EmploymentType { get; set; }
    public bool? Remote { get; set; }
    /// <summary>
    // Find jobs with specific requirements, 
    // specified as a comma delimited list of the following values: 
    //  under_3_years_experience, more_than_3_years_experience, no_experience, no_degree.
    /// <summary>
    public string? JobRequirements { get; set; }
    // Allowed values all, today, 3days, week, month
    public string? DatePosted { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 5;
}
}