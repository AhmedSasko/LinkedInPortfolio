namespace LinkedInPortfolio.API.Models;

public class Skill
{
    public int Id { get; set; }
    public int ProfileSnapshotId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EndorsementCount { get; set; }
}
