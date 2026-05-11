using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInPortfolio.API.Services;

public class ProfileService(AppDbContext db) : IProfileService
{
    public async Task<List<ProfileSummaryDto>> GetAllSummariesAsync()
    {
        return await db.ProfileSnapshots
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .OrderByDescending(p => p.FetchedAt)
            .Select(p => new ProfileSummaryDto
            {
                Id = p.Id,
                FetchedAt = p.FetchedAt,
                Name = p.Name,
                Headline = p.Headline,
                ExperienceCount = p.Experiences.Count,
                EducationCount = p.Educations.Count,
                SkillCount = p.Skills.Count,
                ProjectCount = p.Projects.Count,
                CertificationCount = p.Certifications.Count
            })
            .ToListAsync();
    }

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

        return snapshot is null ? null : MapToDto(snapshot);
    }

    private static ProfileDto MapToDto(ProfileSnapshot s) => new()
    {
        FetchedAt = s.FetchedAt,
        Name = s.Name,
        Headline = s.Headline,
        Location = s.Location,
        About = s.About,
        PhotoBase64 = s.PhotoBase64,
        Experience = s.Experiences.Select(e => new ExperienceDto { Title = e.Title, Company = e.Company, StartDate = e.StartDate, EndDate = e.EndDate, Description = e.Description, IsCurrent = e.IsCurrent }).ToList(),
        Education = s.Educations.Select(e => new EducationDto { School = e.School, Degree = e.Degree, FieldOfStudy = e.FieldOfStudy, StartYear = e.StartYear, EndYear = e.EndYear }).ToList(),
        Skills = s.Skills.Select(sk => new SkillDto { Name = sk.Name, EndorsementCount = sk.EndorsementCount }).ToList(),
        Projects = s.Projects.Select(p => new ProjectDto { Title = p.Title, Description = p.Description, Url = p.Url, StartDate = p.StartDate, EndDate = p.EndDate }).ToList(),
        Certifications = s.Certifications.Select(c => new CertificationDto { Name = c.Name, IssuingOrganization = c.IssuingOrganization, IssueDate = c.IssueDate, CredentialUrl = c.CredentialUrl }).ToList()
    };

    public async Task<ProfileDto?> GetProfileByIdAsync(int id)
    {
        var snapshot = await db.ProfileSnapshots
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (snapshot is null) return null;
        return MapToDto(snapshot);
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

        var existing = await db.ProfileSnapshots.ToListAsync();
        db.ProfileSnapshots.RemoveRange(existing);
        db.ProfileSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
    }
}
