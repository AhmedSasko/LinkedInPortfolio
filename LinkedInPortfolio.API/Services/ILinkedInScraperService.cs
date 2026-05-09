namespace LinkedInPortfolio.API.Services;

public interface ILinkedInScraperService
{
    Task<ProfileData> ScrapeProfileAsync();
}
