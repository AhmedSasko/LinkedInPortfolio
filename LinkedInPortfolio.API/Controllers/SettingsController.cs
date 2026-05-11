using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInPortfolio.API.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(ISettingsService settingsService) : ControllerBase
{
    [HttpGet("linkedin-cookies")]
    public async Task<IActionResult> GetLinkedInCookies()
    {
        var cookies = await settingsService.GetLinkedInCookiesAsync();
        return Ok(new { cookies = cookies ?? string.Empty });
    }

    [HttpPost("linkedin-cookies")]
    public async Task<IActionResult> SetLinkedInCookies([FromBody] SetCookiesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Cookies))
            return BadRequest(new { message = "cookies field must not be empty." });

        await settingsService.SetLinkedInCookiesAsync(request.Cookies);
        return Ok(new { message = "Cookies saved successfully." });
    }
}

public record SetCookiesRequest(string Cookies);
