using PuppeteerSharp;

namespace LinkedInPortfolio.API.Services;

/// <summary>
/// Downloads the Chromium binary once at startup so the first scrape request
/// doesn't pay the ~150 MB download penalty mid-request.
/// </summary>
public sealed class ChromiumDownloaderService(ILogger<ChromiumDownloaderService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Downloading Chromium if needed...");
        await new BrowserFetcher().DownloadAsync();
        logger.LogInformation("Chromium ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
