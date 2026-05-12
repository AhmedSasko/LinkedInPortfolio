using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public interface IGoogleOAuthService
{
    string GetAuthorizationUrl(string state);
    Task<OAuthUserInfo> ExchangeCodeAsync(string code, string redirectUri);
}
