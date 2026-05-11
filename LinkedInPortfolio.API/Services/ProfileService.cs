using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInPortfolio.API.Services;

public class ProfileService(AppDbContext db) : IProfileService
{
    public async Task<ProfileSnapshot> SaveProfileAsync(int userId, ProfileData data)
    {
        var snapshot = new ProfileSnapshot
        {
            UserId = userId,
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
        return snapshot;
    }

    public async Task<ProfileSnapshot?> GetLatestAsync(int userId) =>
        await db.ProfileSnapshots
            .Include(s => s.Experiences)
            .Include(s => s.Educations)
            .Include(s => s.Skills)
            .Include(s => s.Projects)
            .Include(s => s.Certifications)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.FetchedAt)
            .FirstOrDefaultAsync();

    public async Task<ProfileSnapshot?> GetByIdAsync(int userId, int snapshotId) =>
        await db.ProfileSnapshots
            .Include(s => s.Experiences)
            .Include(s => s.Educations)
            .Include(s => s.Skills)
            .Include(s => s.Projects)
            .Include(s => s.Certifications)
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.UserId == userId);

    public async Task<List<ProfileSummaryDto>> GetAllSummariesAsync() =>
        await db.ProfileSnapshots
            .Include(s => s.User)
            .GroupBy(s => s.UserId)
            .Select(g => g.OrderByDescending(s => s.FetchedAt).First())
            .Select(s => new ProfileSummaryDto
            {
                Id = s.Id,
                FetchedAt = s.FetchedAt,
                Name = s.Name,
                Headline = s.Headline,
                UserEmail = s.User.Email
            })
            .ToListAsync();

    public async Task<SyncStatusDto> GetStatusAsync(int userId)
    {
        var latest = await db.ProfileSnapshots
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.FetchedAt)
            .FirstOrDefaultAsync();

        return new SyncStatusDto
        {
            LastSyncedAt = latest?.FetchedAt,
            HasProfile = latest != null
        };
    }
}
