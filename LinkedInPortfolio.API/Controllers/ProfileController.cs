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
public class ProfileController(IProfileService profileService) : ControllerBase
{
    private bool TryGetUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(value, out userId);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var snapshot = await profileService.UpdateProfileAsync(userId, request);
        return Ok(MapToDto(snapshot));
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var snapshot = await profileService.GetLatestAsync(userId);
        if (snapshot is null) return NotFound(new { message = "No profile imported yet." });
        return Ok(MapToDto(snapshot));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var snapshot = await profileService.GetByIdAsync(userId, id);
        if (snapshot is null) return NotFound(new { message = "Snapshot not found." });
        return Ok(MapToDto(snapshot));
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var status = await profileService.GetStatusAsync(userId);
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
        PhotoUrl = s.PhotoUrl,
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
