using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInPortfolio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController(ILinkedInScraperService scraper, IProfileService profileService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Sync()
    {
        try
        {
            var data = await scraper.ScrapeProfileAsync();
            await profileService.SaveProfileAsync(data);
            return Ok(new SyncResultDto
            {
                Success = true,
                SyncedAt = data.FetchedAt,
                Message = "Profile synced successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new SyncResultDto
            {
                Success = false,
                Message = $"Sync failed: {ex.Message}"
            });
        }
    }
}
