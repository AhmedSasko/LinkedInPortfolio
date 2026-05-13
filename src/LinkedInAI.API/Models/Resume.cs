namespace LinkedInAI.API.Models;

public class Resume
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string FileName { get; set; }
    public required string FileType { get; set; }
    public long FileSize { get; set; }
    public required string StoragePath { get; set; }
    public string? ParsedData { get; set; }
    public string Status { get; set; } = "uploaded";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
