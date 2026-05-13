using System.Security.Claims;
using LinkedInAI.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInAI.API.Controllers;

[ApiController]
[Route("api/analysis")]
[Authorize]
public class AnalysisController(IAnalysisService analysisService, ILogger<AnalysisController> logger) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("request")]
    public async Task<IActionResult> RequestAnalysis()
    {
        try
        {
            var result = await analysisService.RequestAnalysisAsync(UserId, "full");
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        var result = await analysisService.GetLatestAsync(UserId);
        if (result == null) return Ok((object?)null);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await analysisService.GetByIdAsync(UserId, id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await analysisService.GetStatusAsync(UserId);
        return Ok(status);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAll()
    {
        var results = await analysisService.GetAllAsync();
        return Ok(results);
    }
}
