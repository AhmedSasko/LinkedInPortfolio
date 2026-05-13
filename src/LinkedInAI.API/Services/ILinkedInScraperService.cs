namespace LinkedInAI.API.Services;

public interface ILinkedInScraperService
{
    Task<ProfileData> ScrapeProfileAsync(string profileUrl, string liAtCookie, Action<string>? onStep = null);
}
