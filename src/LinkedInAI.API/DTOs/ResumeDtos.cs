namespace LinkedInAI.API.DTOs;

public class ResumeDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Status { get; set; } = "uploaded";
    public object? ParsedData { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record ResumeUploadResponse(int ResumeId, string FileName, string Status);
