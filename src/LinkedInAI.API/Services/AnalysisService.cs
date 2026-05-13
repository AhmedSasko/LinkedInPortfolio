using System.Text.Json;
using LinkedInAI.API.Data;
using LinkedInAI.API.DTOs;
using LinkedInAI.API.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace LinkedInAI.API.Services;

public class AnalysisService(
    AppDbContext db,
    IRabbitMqPublisher publisher,
    IDatabase redis,
    ILogger<AnalysisService> logger) : IAnalysisService
{
    private static readonly TimeSpan AnalysisCacheTtl = TimeSpan.FromHours(24);

    public async Task<AnalysisResultDto> RequestAnalysisAsync(int userId, string analysisType = "full")
    {
        var profile = await db.Profiles
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Certifications)
            .Include(p => p.Projects)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No profile found. Please import your LinkedIn profile first.");

        var analysis = new AnalysisResult
        {
            UserId = userId,
            ProfileId = profile.Id,
            AnalysisType = analysisType,
            Status = "pending"
        };
        db.AnalysisResults.Add(analysis);
        await db.SaveChangesAsync();

        // Map to plain DTO to avoid EF navigation property circular references
        var profileData = new ProfileData
        {
            Name = profile.Name,
            Headline = profile.Headline,
            Location = profile.Location,
            About = profile.About,
            PhotoUrl = profile.PhotoUrl,
            Experiences = profile.Experiences.Select(e =>
                new ExperienceData(e.Title, e.Company, e.StartDate, e.EndDate, e.Description, e.IsCurrent)).ToList(),
            Educations = profile.Educations.Select(e =>
                new EducationData(e.School, e.Degree, e.FieldOfStudy, e.StartYear, e.EndYear)).ToList(),
            Skills = profile.Skills.Select(s =>
                new SkillData(s.Name, s.EndorsementCount)).ToList(),
            Certifications = profile.Certifications.Select(c =>
                new CertificationData(c.Name, c.IssuingOrganization, c.IssueDate, c.CredentialUrl)).ToList(),
            Projects = profile.Projects.Select(p =>
                new ProjectData(p.Title, p.Description, p.Url, p.StartDate, p.EndDate)).ToList()
        };

        // Publish to RabbitMQ for async processing — Python service expects snake_case
        var snakeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var message = new
        {
            MessageId = analysis.MessageId,
            UserId = userId,
            ProfileId = profile.Id,
            AnalysisTypes = new[] { analysisType },
            ProfileData = profileData
        };
        await publisher.PublishAsync("analysis.request", JsonSerializer.Serialize(message, snakeOptions));

        logger.LogInformation("Analysis {MessageId} queued for user {UserId}", analysis.MessageId, userId);
        return MapToDto(analysis);
    }

    public async Task<AnalysisResultDto?> GetLatestAsync(int userId)
    {
        // Return cached completed result if available
        var cacheKey = $"analysis:latest:{userId}";
        var cached = await redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return JsonSerializer.Deserialize<AnalysisResultDto>((string)cached!);

        // Return the most recent analysis regardless of status so the frontend
        // can poll while pending/processing
        var analysis = await db.AnalysisResults
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.RequestedAt)
            .FirstOrDefaultAsync();

        if (analysis == null) return null;

        var dto = MapToDto(analysis);

        // Only cache completed results
        if (analysis.Status == "completed")
            await redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(dto), AnalysisCacheTtl);

        return dto;
    }

    public async Task<AnalysisResultDto?> GetByIdAsync(int userId, int analysisId)
    {
        var analysis = await db.AnalysisResults
            .FirstOrDefaultAsync(a => a.Id == analysisId && a.UserId == userId);
        return analysis == null ? null : MapToDto(analysis);
    }

    public async Task<AnalysisStatusDto> GetStatusAsync(int userId)
    {
        var latest = await db.AnalysisResults
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.RequestedAt)
            .FirstOrDefaultAsync();

        var isProcessing = latest?.Status == "pending" || latest?.Status == "processing";
        var lastAnalyzed = await db.AnalysisResults
            .Where(a => a.UserId == userId && a.Status == "completed")
            .OrderByDescending(a => a.CompletedAt)
            .Select(a => (DateTime?)a.CompletedAt)
            .FirstOrDefaultAsync();

        var latestScore = await db.AnalysisResults
            .Where(a => a.UserId == userId && a.Status == "completed")
            .OrderByDescending(a => a.CompletedAt)
            .Select(a => a.OverallScore)
            .FirstOrDefaultAsync();

        return new AnalysisStatusDto(
            HasAnalysis: lastAnalyzed != null,
            IsProcessing: isProcessing,
            LastAnalyzedAt: lastAnalyzed,
            LatestScore: latestScore
        );
    }

    public async Task<List<AnalysisSummaryDto>> GetAllAsync()
    {
        return await db.AnalysisResults
            .Include(a => a.User)
            .OrderByDescending(a => a.RequestedAt)
            .Select(a => new AnalysisSummaryDto(
                a.Id, a.UserId, a.User.Email, a.OverallScore, a.Status, a.RequestedAt, a.CompletedAt))
            .ToListAsync();
    }

    public async Task UpdateResultAsync(string messageId, string status, string? result, int? score, string? error)
    {
        var analysis = await db.AnalysisResults.FirstOrDefaultAsync(a => a.MessageId == messageId);
        if (analysis == null)
        {
            logger.LogWarning("Analysis result for messageId {MessageId} not found", messageId);
            return;
        }

        analysis.Status = status;
        analysis.Result = result;
        analysis.OverallScore = score;
        analysis.ErrorMessage = error;
        analysis.CompletedAt = status == "completed" || status == "failed" ? DateTime.UtcNow : null;

        await db.SaveChangesAsync();

        // Invalidate cache
        await redis.KeyDeleteAsync($"analysis:latest:{analysis.UserId}");
        logger.LogInformation("Analysis {MessageId} updated to status {Status}", messageId, status);
    }

    private static AnalysisResultDto MapToDto(AnalysisResult a)
    {
        object? parsedResult = null;
        if (!string.IsNullOrEmpty(a.Result))
        {
            try { parsedResult = JsonSerializer.Deserialize<JsonElement>(a.Result); }
            catch { parsedResult = a.Result; }
        }

        return new AnalysisResultDto
        {
            Id = a.Id, UserId = a.UserId, ProfileId = a.ProfileId,
            AnalysisType = a.AnalysisType, Status = a.Status,
            Result = parsedResult, OverallScore = a.OverallScore,
            ErrorMessage = a.ErrorMessage, RequestedAt = a.RequestedAt,
            CompletedAt = a.CompletedAt
        };
    }
}
