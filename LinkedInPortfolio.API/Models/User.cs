namespace LinkedInPortfolio.API.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? LinkedInId { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ProfileSnapshot> ProfileSnapshots { get; set; } = new();
}
