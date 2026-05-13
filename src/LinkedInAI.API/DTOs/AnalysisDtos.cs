namespace LinkedInAI.API.DTOs;

public record AnalysisRequestDto(string AnalysisType = "full");

public class AnalysisResultDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProfileId { get; set; }
    public string AnalysisType { get; set; } = "full";
    public string Status { get; set; } = "pending";
    public object? Result { get; set; }
    public int? OverallScore { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public record AnalysisStatusDto(bool HasAnalysis, bool IsProcessing, DateTime? LastAnalyzedAt, int? LatestScore);

public record AnalysisSummaryDto(int Id, int UserId, string? UserEmail, int? OverallScore, string Status, DateTime RequestedAt, DateTime? CompletedAt);
