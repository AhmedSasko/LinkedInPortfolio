namespace LinkedInPortfolio.API.Services;

public interface ISettingsService
{
    Task<string?> GetLinkedInCookiesAsync();
    Task SetLinkedInCookiesAsync(string cookiesJson);
}
