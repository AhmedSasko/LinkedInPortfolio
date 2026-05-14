using LinkedInAI.API.Data;
using LinkedInAI.API.DTOs;
using LinkedInAI.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInAI.API.Services;

public class ProfileService(AppDbContext db, ILogger<ProfileService> logger) : IProfileService
{
    public async Task<ProfileDto?> GetLatestAsync(int userId)
    {
        var profile = await db.Profiles
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Certifications)
            .Include(p => p.Projects)
            .Include(p => p.Languages)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync();

        return profile == null ? null : MapToDto(profile);
    }

    public async Task<ProfileDto?> GetByIdAsync(int userId, int profileId)
    {
        var profile = await db.Profiles
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Certifications)
            .Include(p => p.Projects)
            .Include(p => p.Languages)
            .FirstOrDefaultAsync(p => p.Id == profileId && p.UserId == userId);

        return profile == null ? null : MapToDto(profile);
    }

    public async Task<ProfileDto> UpdateAsync(int userId, UpdateProfileRequest request)
    {
        var profile = await db.Profiles
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Certifications)
            .Include(p => p.Projects)
            .Include(p => p.Languages)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync();

        if (profile == null)
        {
            profile = new Profile { UserId = userId };
            db.Profiles.Add(profile);
        }

        profile.Name = request.Name;
        profile.Headline = request.Headline;
        profile.Location = request.Location;
        profile.About = request.About;
        profile.PhotoUrl = request.PhotoUrl;
        profile.FetchedAt = DateTime.UtcNow;

        db.ProfileExperiences.RemoveRange(profile.Experiences);
        db.ProfileEducations.RemoveRange(profile.Educations);
        db.ProfileSkills.RemoveRange(profile.Skills);
        db.ProfileCertifications.RemoveRange(profile.Certifications);
        db.ProfileProjects.RemoveRange(profile.Projects);
        db.ProfileLanguages.RemoveRange(profile.Languages);

        profile.Experiences = request.Experiences.Select(e => new ProfileExperience
        {
            Title = e.Title, Company = e.Company, StartDate = e.StartDate,
            EndDate = e.EndDate, Description = e.Description, IsCurrent = e.IsCurrent
        }).ToList();

        profile.Educations = request.Educations.Select(e => new ProfileEducation
        {
            School = e.School, Degree = e.Degree, FieldOfStudy = e.FieldOfStudy,
            StartYear = e.StartYear, EndYear = e.EndYear
        }).ToList();

        profile.Skills = request.Skills.Select(s => new ProfileSkill
        {
            Name = s.Name, EndorsementCount = s.EndorsementCount
        }).ToList();

        profile.Certifications = request.Certifications.Select(c => new ProfileCertification
        {
            Name = c.Name, IssuingOrganization = c.IssuingOrganization,
            IssueDate = c.IssueDate, CredentialUrl = c.CredentialUrl
        }).ToList();

        profile.Projects = request.Projects.Select(p => new ProfileProject
        {
            Title = p.Title, Description = p.Description,
            Url = p.Url, StartDate = p.StartDate, EndDate = p.EndDate
        }).ToList();

        profile.Languages = request.Languages.Select(l => new ProfileLanguage
        {
            Name = l.Name, Proficiency = l.Proficiency
        }).ToList();

        await db.SaveChangesAsync();
        logger.LogInformation("Profile updated for user {UserId}", userId);
        return MapToDto(profile);
    }

    public async Task<ProfileDto> SaveScrapedAsync(int userId, ProfileData data)
    {
        // Save the parent profile first to get its Id, then save children with
        // explicit ProfileId. This avoids FK constraint failures caused by the
        // unidirectional WithMany() relationship configuration in OnModelCreating.
        var profile = new Profile
        {
            UserId = userId,
            Name = data.Name,
            Headline = data.Headline,
            Location = data.Location,
            About = data.About,
            PhotoUrl = data.PhotoUrl,
            PhotoBase64 = data.PhotoBase64,
            FetchedAt = DateTime.UtcNow,
        };

        db.Profiles.Add(profile);
        await db.SaveChangesAsync(); // profile.Id is now populated

        var experiences = data.Experiences.Select(e => new ProfileExperience
        {
            ProfileId = profile.Id,
            Title = e.Title, Company = e.Company, StartDate = e.StartDate,
            EndDate = e.EndDate, Description = e.Description, IsCurrent = e.IsCurrent
        }).ToList();

        var educations = data.Educations.Select(e => new ProfileEducation
        {
            ProfileId = profile.Id,
            School = e.School, Degree = e.Degree, FieldOfStudy = e.FieldOfStudy,
            StartYear = e.StartYear, EndYear = e.EndYear
        }).ToList();

        var skills = data.Skills.Select(s => new ProfileSkill
        {
            ProfileId = profile.Id,
            Name = s.Name, EndorsementCount = s.EndorsementCount
        }).ToList();

        var certifications = data.Certifications.Select(c => new ProfileCertification
        {
            ProfileId = profile.Id,
            Name = c.Name, IssuingOrganization = c.IssuingOrganization,
            IssueDate = c.IssueDate, CredentialUrl = c.CredentialUrl
        }).ToList();

        var projects = data.Projects.Select(p => new ProfileProject
        {
            ProfileId = profile.Id,
            Title = p.Title, Description = p.Description,
            Url = p.Url, StartDate = p.StartDate, EndDate = p.EndDate
        }).ToList();

        var languages = data.Languages.Select(l => new ProfileLanguage
        {
            ProfileId = profile.Id,
            Name = l.Name, Proficiency = l.Proficiency
        }).ToList();

        if (experiences.Count > 0) db.ProfileExperiences.AddRange(experiences);
        if (educations.Count > 0)  db.ProfileEducations.AddRange(educations);
        if (skills.Count > 0)      db.ProfileSkills.AddRange(skills);
        if (certifications.Count > 0) db.ProfileCertifications.AddRange(certifications);
        if (projects.Count > 0)    db.ProfileProjects.AddRange(projects);
        if (languages.Count > 0)   db.ProfileLanguages.AddRange(languages);

        await db.SaveChangesAsync();

        profile.Experiences = experiences;
        profile.Educations = educations;
        profile.Skills = skills;
        profile.Certifications = certifications;
        profile.Projects = projects;
        profile.Languages = languages;

        logger.LogInformation("Scraped profile saved for user {UserId}", userId);
        return MapToDto(profile);
    }

    public async Task<ProfileStatusDto> GetStatusAsync(int userId)
    {
        var profile = await db.Profiles
            .Include(p => p.Experiences)
            .Include(p => p.Skills)
            .Include(p => p.Educations)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .Include(p => p.Languages)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync();

        if (profile == null)
            return new ProfileStatusDto(false, null, 0, 0, 0, 0, 0, 0);

        return new ProfileStatusDto(
            HasProfile: true,
            LastSyncedAt: profile.FetchedAt,
            ExperienceCount: profile.Experiences.Count,
            SkillCount: profile.Skills.Count,
            EducationCount: profile.Educations.Count,
            ProjectCount: profile.Projects.Count,
            CertificationCount: profile.Certifications.Count,
            LanguageCount: profile.Languages.Count
        );
    }

    public async Task<List<ProfileSummaryDto>> GetAllSummariesAsync()
    {
        var latest = await db.Profiles
            .GroupBy(p => p.UserId)
            .Select(g => g.OrderByDescending(p => p.FetchedAt).First())
            .Select(p => new ProfileSummaryDto(p.Id, p.UserId, p.Name, p.Headline, p.PhotoUrl, p.FetchedAt))
            .ToListAsync();

        return latest;
    }

    public async Task SyncFromLinkedInAsync(int userId, string? name, string? photoUrl)
    {
        var profile = await db.Profiles
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync();

        if (profile == null)
        {
            profile = new Profile { UserId = userId };
            db.Profiles.Add(profile);
        }

        if (!string.IsNullOrEmpty(name)) profile.Name = name;
        if (!string.IsNullOrEmpty(photoUrl)) profile.PhotoUrl = photoUrl;
        profile.FetchedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("LinkedIn OAuth sync completed for user {UserId}", userId);
    }

    private static ProfileDto MapToDto(Profile p) => new()
    {
        Id = p.Id, UserId = p.UserId, Name = p.Name, Headline = p.Headline,
        Location = p.Location, About = p.About, PhotoUrl = p.PhotoUrl,
        PhotoBase64 = p.PhotoBase64, FetchedAt = p.FetchedAt,
        Experiences = p.Experiences.Select(e => new ExperienceDto(
            e.Id, e.Title, e.Company, e.StartDate, e.EndDate, e.Description, e.IsCurrent)).ToList(),
        Educations = p.Educations.Select(e => new EducationDto(
            e.Id, e.School, e.Degree, e.FieldOfStudy, e.StartYear, e.EndYear)).ToList(),
        Skills = p.Skills.Select(s => new SkillDto(s.Id, s.Name, s.EndorsementCount)).ToList(),
        Certifications = p.Certifications.Select(c => new CertificationDto(
            c.Id, c.Name, c.IssuingOrganization, c.IssueDate, c.CredentialUrl)).ToList(),
        Projects = p.Projects.Select(pr => new ProjectDto(
            pr.Id, pr.Title, pr.Description, pr.Url, pr.StartDate, pr.EndDate)).ToList(),
        Languages = p.Languages.Select(l => new LanguageDto(l.Id, l.Name, l.Proficiency)).ToList()
    };
}
