namespace LinkedInPortfolio.API.DTOs;

public class SyncResultDto
{
    public bool Success { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
