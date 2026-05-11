using System.Security.Claims;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Models;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInPortfolio.API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController(IProfileService profileService, ILinkedInScraperService scraperService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException());

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LinkedInUrl) ||
            !request.LinkedInUrl.Contains("linkedin.com/in/"))
            return BadRequest(new { message = "Please provide a valid LinkedIn profile URL (e.g. https://www.linkedin.com/in/username)." });

        try
        {
            var data = await scraperService.ScrapeProfileAsync(request.LinkedInUrl);
            var snapshot = await profileService.SaveProfileAsync(UserId, data);
            return Ok(MapToDto(snapshot));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        var snapshot = await profileService.GetLatestAsync(UserId);
        if (snapshot is null) return NotFound(new { message = "No profile imported yet." });
        return Ok(MapToDto(snapshot));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var snapshot = await profileService.GetByIdAsync(UserId, id);
        if (snapshot is null) return NotFound();
        return Ok(MapToDto(snapshot));
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await profileService.GetStatusAsync(UserId);
        return Ok(status);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAll()
    {
        var summaries = await profileService.GetAllSummariesAsync();
        return Ok(summaries);
    }

    private static ProfileDto MapToDto(ProfileSnapshot s) => new()
    {
        Id = s.Id,
        FetchedAt = s.FetchedAt,
        Name = s.Name,
        Headline = s.Headline,
        Location = s.Location,
        About = s.About,
        PhotoBase64 = s.PhotoBase64,
        Experiences = s.Experiences.Select(e => new ExperienceDto
        {
            Title = e.Title,
            Company = e.Company,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Description = e.Description,
            IsCurrent = e.IsCurrent
        }).ToList(),
        Educations = s.Educations.Select(e => new EducationDto
        {
            School = e.School,
            Degree = e.Degree,
            FieldOfStudy = e.FieldOfStudy,
            StartYear = e.StartYear,
            EndYear = e.EndYear
        }).ToList(),
        Skills = s.Skills.Select(sk => new SkillDto
        {
            Name = sk.Name,
            EndorsementCount = sk.EndorsementCount
        }).ToList(),
        Projects = s.Projects.Select(p => new ProjectDto
        {
            Title = p.Title,
            Description = p.Description,
            Url = p.Url,
            StartDate = p.StartDate,
            EndDate = p.EndDate
        }).ToList(),
        Certifications = s.Certifications.Select(c => new CertificationDto
        {
            Name = c.Name,
            IssuingOrganization = c.IssuingOrganization,
            IssueDate = c.IssueDate,
            CredentialUrl = c.CredentialUrl
        }).ToList()
    };
}
