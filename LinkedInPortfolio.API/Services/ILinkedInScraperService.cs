namespace LinkedInPortfolio.API.Services;

public interface ILinkedInScraperService
{
    Task<ProfileData> ScrapeProfileAsync(string profileUrl, string liAtCookie);
}
