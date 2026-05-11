using PuppeteerSharp;

namespace LinkedInPortfolio.API.Services;

/// <summary>
/// Downloads the Chromium binary in the background at startup so the first
/// scrape request doesn't pay the ~150 MB download penalty mid-request.
/// Inherits BackgroundService so ExecuteAsync runs via Task.Run and does NOT
/// block the HTTP server from starting.
/// </summary>
public sealed class ChromiumDownloaderService(ILogger<ChromiumDownloaderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Downloading Chromium if needed...");
        await new BrowserFetcher().DownloadAsync();
        logger.LogInformation("Chromium ready.");
    }
}
