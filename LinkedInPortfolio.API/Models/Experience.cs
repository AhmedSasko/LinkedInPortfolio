namespace LinkedInPortfolio.API.Models;

public class Experience
{
    public int Id { get; set; }
    public int ProfileSnapshotId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}
