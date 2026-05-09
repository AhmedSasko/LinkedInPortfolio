using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInPortfolio.API.Services;

public class ProfileService(AppDbContext db) : IProfileService
{
    public async Task<ProfileDto?> GetProfileAsync()
    {
        var snapshot = await db.ProfileSnapshots
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync();

        if (snapshot is null) return null;

        return new ProfileDto
        {
            FetchedAt = snapshot.FetchedAt,
            Name = snapshot.Name,
            Headline = snapshot.Headline,
            Location = snapshot.Location,
            About = snapshot.About,
            PhotoBase64 = snapshot.PhotoBase64,
            Experience = snapshot.Experiences.Select(e => new ExperienceDto
            {
                Title = e.Title,
                Company = e.Company,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Description = e.Description,
                IsCurrent = e.IsCurrent
            }).ToList(),
            Education = snapshot.Educations.Select(e => new EducationDto
            {
                School = e.School,
                Degree = e.Degree,
                FieldOfStudy = e.FieldOfStudy,
                StartYear = e.StartYear,
                EndYear = e.EndYear
            }).ToList(),
            Skills = snapshot.Skills.Select(s => new SkillDto
            {
                Name = s.Name,
                EndorsementCount = s.EndorsementCount
            }).ToList(),
            Projects = snapshot.Projects.Select(p => new ProjectDto
            {
                Title = p.Title,
                Description = p.Description,
                Url = p.Url,
                StartDate = p.StartDate,
                EndDate = p.EndDate
            }).ToList(),
            Certifications = snapshot.Certifications.Select(c => new CertificationDto
            {
                Name = c.Name,
                IssuingOrganization = c.IssuingOrganization,
                IssueDate = c.IssueDate,
                CredentialUrl = c.CredentialUrl
            }).ToList()
        };
    }

    public async Task<ProfileStatusDto> GetStatusAsync()
    {
        var snapshot = await db.ProfileSnapshots
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync();

        if (snapshot is null)
            return new ProfileStatusDto();

        return new ProfileStatusDto
        {
            LastSyncedAt = snapshot.FetchedAt,
            ExperienceCount = snapshot.Experiences.Count,
            EducationCount = snapshot.Educations.Count,
            SkillCount = snapshot.Skills.Count,
            ProjectCount = snapshot.Projects.Count,
            CertificationCount = snapshot.Certifications.Count
        };
    }

    public async Task SaveProfileAsync(ProfileData data)
    {
        var existing = await db.ProfileSnapshots.ToListAsync();
        db.ProfileSnapshots.RemoveRange(existing);
        await db.SaveChangesAsync();

        var snapshot = new ProfileSnapshot
        {
            FetchedAt = data.FetchedAt,
            Name = data.Name,
            Headline = data.Headline,
            Location = data.Location,
            About = data.About,
            PhotoBase64 = data.PhotoBase64,
            Experiences = data.Experiences.Select(e => new Experience
            {
                Title = e.Title,
                Company = e.Company,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Description = e.Description,
                IsCurrent = e.IsCurrent
            }).ToList(),
            Educations = data.Educations.Select(e => new Education
            {
                School = e.School,
                Degree = e.Degree,
                FieldOfStudy = e.FieldOfStudy,
                StartYear = e.StartYear,
                EndYear = e.EndYear
            }).ToList(),
            Skills = data.Skills.Select(s => new Skill
            {
                Name = s.Name,
                EndorsementCount = s.EndorsementCount
            }).ToList(),
            Projects = data.Projects.Select(p => new Project
            {
                Title = p.Title,
                Description = p.Description,
                Url = p.Url,
                StartDate = p.StartDate,
                EndDate = p.EndDate
            }).ToList(),
            Certifications = data.Certifications.Select(c => new Certification
            {
                Name = c.Name,
                IssuingOrganization = c.IssuingOrganization,
                IssueDate = c.IssueDate,
                CredentialUrl = c.CredentialUrl
            }).ToList()
        };

        db.ProfileSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
    }
}
