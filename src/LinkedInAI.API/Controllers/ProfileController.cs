using System.Security.Claims;
using LinkedInAI.API.DTOs;
using LinkedInAI.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInAI.API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController(
    IProfileService profileService,
    IServiceScopeFactory scopeFactory,
    ScrapeProgressStore progressStore,
    ILogger<ProfileController> logger) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        var profile = await profileService.GetLatestAsync(UserId);
        if (profile == null) return Ok((object?)null);
        return Ok(profile);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var profile = await profileService.GetByIdAsync(UserId, id);
        if (profile == null) return NotFound();
        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request)
    {
        var profile = await profileService.UpdateAsync(UserId, request);
        return Ok(profile);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await profileService.GetStatusAsync(UserId);
        return Ok(status);
    }

    [HttpPost("scrape")]
    public IActionResult StartScrape([FromBody] ScrapeRequest request)
    {
        var userId = UserId;

        if (progressStore.IsRunning(userId))
            return Conflict(new { error = "A scrape is already in progress" });

        progressStore.Start(userId);

        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var scraper = scope.ServiceProvider.GetRequiredService<ILinkedInScraperService>();
            var profileSvc = scope.ServiceProvider.GetRequiredService<IProfileService>();

            try
            {
                var data = await scraper.ScrapeProfileAsync(
                    request.LinkedInUrl,
                    request.LiAtCookie,
                    step => progressStore.SetStep(userId, step)
                );

                progressStore.SetStep(userId, "saving");
                var profile = await profileSvc.SaveScrapedAsync(userId, data);
                progressStore.Complete(userId, profile);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Scrape failed for user {UserId}: {Message}", userId, ex.Message);
                progressStore.Fail(userId, ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background scrape failed for user {UserId}", userId);
                progressStore.Fail(userId, "Scraping failed. Please try again.");
            }
        });

        return Accepted(new { message = "Scraping started" });
    }

    [HttpGet("scrape-progress")]
    public IActionResult GetScrapeProgress()
    {
        var progress = progressStore.Get(UserId);
        if (progress == null)
            return Ok(new ScrapeProgressDto(false, [], null, null));
        return Ok(progress);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAll()
    {
        var summaries = await profileService.GetAllSummariesAsync();
        return Ok(summaries);
    }
}
