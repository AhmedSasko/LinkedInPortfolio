using System.Text.Json;
using LinkedInAI.API.Data;
using LinkedInAI.API.DTOs;
using LinkedInAI.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LinkedInAI.API.Services;

public class ResumeService(
    AppDbContext db,
    IRabbitMqPublisher publisher,
    IWebHostEnvironment env,
    ILogger<ResumeService> logger) : IResumeService
{
    private static readonly string[] AllowedTypes = ["application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"];

    public async Task<ResumeUploadResponse> UploadAsync(int userId, IFormFile file)
    {
        if (!AllowedTypes.Contains(file.ContentType))
            throw new InvalidOperationException("Only PDF and DOCX files are supported");

        if (file.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("File size must be under 10MB");

        var uploadsDir = Path.Combine(env.ContentRootPath, "uploads", userId.ToString());
        Directory.CreateDirectory(uploadsDir);

        var fileType = file.ContentType.Contains("pdf") ? "pdf" : "docx";
        var fileName = $"{Guid.NewGuid():N}.{fileType}";
        var storagePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = File.Create(storagePath))
            await file.CopyToAsync(stream);

        var resume = new Resume
        {
            UserId = userId,
            FileName = file.FileName,
            FileType = fileType,
            FileSize = file.Length,
            StoragePath = storagePath,
            Status = "uploaded"
        };

        db.Resumes.Add(resume);
        await db.SaveChangesAsync();

        // Publish parse request to RabbitMQ — Python service expects snake_case
        var fileBytes = await File.ReadAllBytesAsync(storagePath);
        var snakeOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var message = JsonSerializer.Serialize(new
        {
            MessageId = resume.Id.ToString(),
            UserId = userId,
            ResumeId = resume.Id,
            FileName = resume.FileName,
            FileContentBase64 = Convert.ToBase64String(fileBytes),
            FileType = resume.FileType
        }, snakeOptions);
        await publisher.PublishAsync("resume.parse.request", message);

        resume.Status = "parsing";
        await db.SaveChangesAsync();

        logger.LogInformation("Resume {Id} uploaded and queued for parsing", resume.Id);
        return new ResumeUploadResponse(resume.Id, resume.FileName, resume.Status);
    }

    public async Task<List<ResumeDto>> GetUploadsAsync(int userId)
    {
        return await db.Resumes
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapToDto(r))
            .ToListAsync();
    }

    public async Task<ResumeDto?> GetByIdAsync(int userId, int resumeId)
    {
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == resumeId && r.UserId == userId);
        return resume == null ? null : MapToDto(resume);
    }

    public async Task<ProfileDto> ImportToProfileAsync(int userId, int resumeId)
    {
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == resumeId && r.UserId == userId)
            ?? throw new InvalidOperationException("Resume not found");

        if (resume.Status != "parsed" || string.IsNullOrEmpty(resume.ParsedData))
            throw new InvalidOperationException("Resume has not been parsed yet");

        var parsed = JsonSerializer.Deserialize<ParsedResumeData>(resume.ParsedData,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (parsed == null) throw new InvalidOperationException("Failed to read parsed resume data");

        var request = new UpdateProfileRequest
        {
            Name = parsed.Name,
            About = parsed.Summary,
            Skills = (parsed.Skills ?? []).Select(s => new SkillRequest(s)).ToList(),
            Experiences = (parsed.Experiences ?? []).Select(e => new ExperienceRequest(
                e.Title, e.Company, e.StartDate, e.EndDate, e.Description, e.IsCurrent)).ToList(),
            Educations = (parsed.Educations ?? []).Select(e => new EducationRequest(
                e.School, e.Degree, e.FieldOfStudy, e.StartYear, e.EndYear)).ToList(),
            Certifications = (parsed.Certifications ?? []).Select(c => new CertificationRequest(
                c.Name, c.IssuingOrganization, c.IssueDate, null)).ToList()
        };

        var profileService = new ProfileService(db, logger as ILogger<ProfileService>
            ?? throw new InvalidOperationException());
        return await profileService.UpdateAsync(userId, request);
    }

    public async Task UpdateParseResultAsync(int resumeId, string status, string? parsedData, string? error)
    {
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == resumeId);
        if (resume == null)
        {
            logger.LogWarning("Resume {Id} not found for parse result update", resumeId);
            return;
        }

        resume.Status = status;
        resume.ParsedData = parsedData;
        await db.SaveChangesAsync();
        logger.LogInformation("Resume {Id} parse result updated to status {Status}", resumeId, status);
    }

    private static ResumeDto MapToDto(Resume r)
    {
        object? parsedData = null;
        if (!string.IsNullOrEmpty(r.ParsedData))
        {
            try { parsedData = JsonSerializer.Deserialize<JsonElement>(r.ParsedData); }
            catch { parsedData = r.ParsedData; }
        }

        return new ResumeDto
        {
            Id = r.Id, FileName = r.FileName, FileType = r.FileType,
            FileSize = r.FileSize, Status = r.Status, ParsedData = parsedData,
            CreatedAt = r.CreatedAt
        };
    }
}

// Minimal parsed resume data schema matching what Python AI service returns
public class ParsedResumeData
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Summary { get; set; }
    public List<string>? Skills { get; set; }
    public List<ParsedExperience>? Experiences { get; set; }
    public List<ParsedEducation>? Educations { get; set; }
    public List<ParsedCertification>? Certifications { get; set; }
}

public class ParsedExperience
{
    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Description { get; set; }
    public bool IsCurrent { get; set; }
}

public class ParsedEducation
{
    public string? School { get; set; }
    public string? Degree { get; set; }
    public string? FieldOfStudy { get; set; }
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }
}

public class ParsedCertification
{
    public string? Name { get; set; }
    public string? IssuingOrganization { get; set; }
    public string? IssueDate { get; set; }
}
