namespace DDEyC_API.Models.DTOs
{
   public class JobListingFilter
{
    public string? Title { get; set; }
    public string? CompanyName { get; set; }
    public string? CountryCode { get; set; } = "MX";
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? EmploymentType { get; set; }
    public bool? Remote { get; set; }
    public string? JobRequirements { get; set; }
    public string? DatePosted { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 5;
}
}