namespace LinkedInPortfolio.API.DTOs;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
}
