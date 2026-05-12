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
            FetchedAt = data.FetchedAt == default ? DateTime.UtcNow : data.FetchedAt,
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
        await db.Users
            .Select(u => new
            {
                u.Email,
                Latest = u.ProfileSnapshots
                    .OrderByDescending(s => s.FetchedAt)
                    .FirstOrDefault()
            })
            .Where(u => u.Latest != null)
            .Select(u => new ProfileSummaryDto
            {
                Id = u.Latest!.Id,
                FetchedAt = u.Latest.FetchedAt,
                Name = u.Latest.Name,
                Headline = u.Latest.Headline,
                UserEmail = u.Email
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

    public async Task<ProfileSnapshot> UpdateProfileAsync(int userId, UpdateProfileRequest request)
    {
        var snapshot = await db.ProfileSnapshots
            .Include(s => s.Experiences)
            .Include(s => s.Educations)
            .Include(s => s.Skills)
            .Include(s => s.Projects)
            .Include(s => s.Certifications)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.FetchedAt)
            .FirstOrDefaultAsync();

        if (snapshot is null)
        {
            snapshot = new ProfileSnapshot
            {
                UserId = userId,
                FetchedAt = DateTime.UtcNow,
                Name = request.Name,
                Headline = request.Headline,
                Location = request.Location,
                About = request.About,
                PhotoUrl = request.PhotoUrl,
                Experiences = request.Experiences.Select(e => new Experience
                {
                    Title = e.Title,
                    Company = e.Company,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    Description = e.Description,
                    IsCurrent = e.IsCurrent
                }).ToList(),
                Educations = request.Educations.Select(e => new Education
                {
                    School = e.School,
                    Degree = e.Degree,
                    FieldOfStudy = e.FieldOfStudy,
                    StartYear = e.StartYear,
                    EndYear = e.EndYear
                }).ToList(),
                Skills = request.Skills.Select(s => new Skill
                {
                    Name = s.Name,
                    EndorsementCount = s.EndorsementCount
                }).ToList(),
                Projects = request.Projects.Select(p => new Project
                {
                    Title = p.Title,
                    Description = p.Description,
                    Url = p.Url,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate
                }).ToList(),
                Certifications = request.Certifications.Select(c => new Certification
                {
                    Name = c.Name,
                    IssuingOrganization = c.IssuingOrganization,
                    IssueDate = c.IssueDate,
                    CredentialUrl = c.CredentialUrl
                }).ToList()
            };
            db.ProfileSnapshots.Add(snapshot);
        }
        else
        {
            snapshot.Name = request.Name;
            snapshot.Headline = request.Headline;
            snapshot.Location = request.Location;
            snapshot.About = request.About;
            snapshot.PhotoUrl = request.PhotoUrl;
            snapshot.FetchedAt = DateTime.UtcNow;

            snapshot.Experiences.Clear();
            snapshot.Educations.Clear();
            snapshot.Skills.Clear();
            snapshot.Projects.Clear();
            snapshot.Certifications.Clear();

            foreach (var e in request.Experiences)
                snapshot.Experiences.Add(new Experience
                {
                    Title = e.Title,
                    Company = e.Company,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    Description = e.Description,
                    IsCurrent = e.IsCurrent
                });

            foreach (var e in request.Educations)
                snapshot.Educations.Add(new Education
                {
                    School = e.School,
                    Degree = e.Degree,
                    FieldOfStudy = e.FieldOfStudy,
                    StartYear = e.StartYear,
                    EndYear = e.EndYear
                });

            foreach (var s in request.Skills)
                snapshot.Skills.Add(new Skill
                {
                    Name = s.Name,
                    EndorsementCount = s.EndorsementCount
                });

            foreach (var p in request.Projects)
                snapshot.Projects.Add(new Project
                {
                    Title = p.Title,
                    Description = p.Description,
                    Url = p.Url,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate
                });

            foreach (var c in request.Certifications)
                snapshot.Certifications.Add(new Certification
                {
                    Name = c.Name,
                    IssuingOrganization = c.IssuingOrganization,
                    IssueDate = c.IssueDate,
                    CredentialUrl = c.CredentialUrl
                });
        }

        await db.SaveChangesAsync();
        return snapshot;
    }
}
