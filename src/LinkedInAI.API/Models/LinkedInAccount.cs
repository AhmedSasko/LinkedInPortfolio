namespace LinkedInAI.API.Models;

public class LinkedInAccount
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string LinkedInId { get; set; }
    public required string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime TokenExpiry { get; set; }
    public string? ProfileJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
