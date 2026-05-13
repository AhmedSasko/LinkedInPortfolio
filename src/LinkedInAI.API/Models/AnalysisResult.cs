namespace LinkedInAI.API.Models;

public class AnalysisResult
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProfileId { get; set; }
    public string AnalysisType { get; set; } = "full";
    public string Status { get; set; } = "pending";
    public string? Result { get; set; }
    public int? OverallScore { get; set; }
    public string? ErrorMessage { get; set; }
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public Profile Profile { get; set; } = null!;
}
