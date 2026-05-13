using LinkedInAI.API.DTOs;

namespace LinkedInAI.API.Services;

public interface IOAuthService
{
    string GetAuthorizationUrl(string state);
    Task<OAuthUserInfo> ExchangeCodeAsync(string code, string redirectUri);
}
