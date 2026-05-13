namespace LinkedInAI.API.Models;

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public LinkedInAccount? LinkedInAccount { get; set; }
    public List<Profile> Profiles { get; set; } = [];
    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<Resume> Resumes { get; set; } = [];
    public List<AnalysisResult> AnalysisResults { get; set; } = [];
}
