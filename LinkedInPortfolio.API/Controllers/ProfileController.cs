using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInPortfolio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController(IProfileService profileService) : ControllerBase
{
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var summaries = await profileService.GetAllSummariesAsync();
        return Ok(summaries);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var profile = await profileService.GetProfileByIdAsync(id);
        if (profile is null) return NotFound();
        return Ok(profile);
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var profile = await profileService.GetProfileAsync();
        if (profile is null) return NoContent();
        return Ok(profile);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await profileService.GetStatusAsync();
        return Ok(status);
    }
}
