using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(string email, string password);
    Task<AuthResponseDto?> LoginAsync(string email, string password);
}
