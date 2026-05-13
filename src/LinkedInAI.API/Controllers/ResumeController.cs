using System.Security.Claims;
using LinkedInAI.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInAI.API.Controllers;

[ApiController]
[Route("api/resume")]
[Authorize]
public class ResumeController(IResumeService resumeService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        try
        {
            var result = await resumeService.UploadAsync(UserId, file);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("uploads")]
    public async Task<IActionResult> GetUploads()
    {
        var resumes = await resumeService.GetUploadsAsync(UserId);
        return Ok(resumes);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var resume = await resumeService.GetByIdAsync(UserId, id);
        if (resume == null) return NotFound();
        return Ok(resume);
    }

    [HttpPost("{id:int}/import")]
    public async Task<IActionResult> ImportToProfile(int id)
    {
        try
        {
            var profile = await resumeService.ImportToProfileAsync(UserId, id);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
