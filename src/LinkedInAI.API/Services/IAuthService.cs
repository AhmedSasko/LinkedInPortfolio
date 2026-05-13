using LinkedInAI.API.DTOs;

namespace LinkedInAI.API.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken, int userId);
    Task<AuthResponse> HandleOAuthLoginAsync(OAuthUserInfo userInfo, string? linkedInId = null);
}
