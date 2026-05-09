namespace LinkedInPortfolio.API.Models;

public class Project
{
    public int Id { get; set; }
    public int ProfileSnapshotId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}
