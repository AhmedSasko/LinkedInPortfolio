namespace LinkedInPortfolio.API.Models;

public class Certification
{
    public int Id { get; set; }
    public int ProfileSnapshotId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string CredentialUrl { get; set; } = string.Empty;
}
