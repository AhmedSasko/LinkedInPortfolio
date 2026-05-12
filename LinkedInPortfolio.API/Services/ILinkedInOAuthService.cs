using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public interface ILinkedInOAuthService
{
    string GetAuthorizationUrl(string state);
    Task<LinkedInUserInfo> ExchangeCodeAsync(string code, string redirectUri);
}
