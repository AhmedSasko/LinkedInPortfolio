using FluentValidation;

namespace LinkedInAI.API.DTOs;

public record ScrapeRequest(string LinkedInUrl, string LiAtCookie);

public class UpdateProfileRequest
{
    public string? Name { get; set; }
    public string? Headline { get; set; }
    public string? Location { get; set; }
    public string? About { get; set; }
    public string? PhotoUrl { get; set; }
    public List<ExperienceRequest> Experiences { get; set; } = [];
    public List<EducationRequest> Educations { get; set; } = [];
    public List<SkillRequest> Skills { get; set; } = [];
    public List<CertificationRequest> Certifications { get; set; } = [];
    public List<ProjectRequest> Projects { get; set; } = [];
    public List<LanguageRequest> Languages { get; set; } = [];
}

public record ExperienceRequest(string? Title, string? Company, string? StartDate, string? EndDate, string? Description, bool IsCurrent);
public record EducationRequest(string? School, string? Degree, string? FieldOfStudy, string? StartYear, string? EndYear);
public record SkillRequest(string Name, int EndorsementCount = 0);
public record CertificationRequest(string? Name, string? IssuingOrganization, string? IssueDate, string? CredentialUrl);
public record ProjectRequest(string? Title, string? Description, string? Url, string? StartDate, string? EndDate);
public record LanguageRequest(string Name, string? Proficiency);

public class ProfileDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Name { get; set; }
    public string? Headline { get; set; }
    public string? Location { get; set; }
    public string? About { get; set; }
    public string? PhotoUrl { get; set; }
    public string? PhotoBase64 { get; set; }
    public DateTime FetchedAt { get; set; }
    public List<ExperienceDto> Experiences { get; set; } = [];
    public List<EducationDto> Educations { get; set; } = [];
    public List<SkillDto> Skills { get; set; } = [];
    public List<CertificationDto> Certifications { get; set; } = [];
    public List<ProjectDto> Projects { get; set; } = [];
    public List<LanguageDto> Languages { get; set; } = [];
}

public record ExperienceDto(int Id, string? Title, string? Company, string? StartDate, string? EndDate, string? Description, bool IsCurrent);
public record EducationDto(int Id, string? School, string? Degree, string? FieldOfStudy, string? StartYear, string? EndYear);
public record SkillDto(int Id, string Name, int EndorsementCount);
public record CertificationDto(int Id, string? Name, string? IssuingOrganization, string? IssueDate, string? CredentialUrl);
public record ProjectDto(int Id, string? Title, string? Description, string? Url, string? StartDate, string? EndDate);
public record LanguageDto(int Id, string Name, string? Proficiency);

public record ProfileSummaryDto(int Id, int UserId, string? Name, string? Headline, string? PhotoUrl, DateTime FetchedAt);

public record ProfileStatusDto(
    bool HasProfile,
    DateTime? LastSyncedAt,
    int ExperienceCount,
    int SkillCount,
    int EducationCount,
    int ProjectCount,
    int CertificationCount,
    int LanguageCount
);

public record ScrapeStepDto(string Key, string Label, string Status);
public record ScrapeProgressDto(bool IsRunning, List<ScrapeStepDto> Steps, string? Error, ProfileDto? Result);

public class ScrapeRequestValidator : AbstractValidator<ScrapeRequest>
{
    public ScrapeRequestValidator()
    {
        RuleFor(x => x.LinkedInUrl)
            .NotEmpty()
            .Must(url =>
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
                return uri.Host.EndsWith("linkedin.com", StringComparison.OrdinalIgnoreCase)
                    && uri.AbsolutePath.StartsWith("/in/", StringComparison.OrdinalIgnoreCase);
            })
            .WithMessage("Must be a valid LinkedIn profile URL (e.g. https://www.linkedin.com/in/username)");

        RuleFor(x => x.LiAtCookie).NotEmpty().WithMessage("LinkedIn session cookie is required");
    }
}
