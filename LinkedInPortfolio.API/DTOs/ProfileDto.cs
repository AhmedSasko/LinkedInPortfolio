namespace LinkedInPortfolio.API.DTOs;

public class UpdateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string About { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public List<ExperienceDto> Experiences { get; set; } = new();
    public List<EducationDto> Educations { get; set; } = new();
    public List<SkillDto> Skills { get; set; } = new();
    public List<ProjectDto> Projects { get; set; } = new();
    public List<CertificationDto> Certifications { get; set; } = new();
}

public class ProfileSummaryDto
{
    public int Id { get; set; }
    public DateTime FetchedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
}

public class ProfileDto
{
    public int Id { get; set; }
    public DateTime FetchedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string About { get; set; } = string.Empty;
    public string PhotoBase64 { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public List<ExperienceDto> Experiences { get; set; } = new();
    public List<EducationDto> Educations { get; set; } = new();
    public List<SkillDto> Skills { get; set; } = new();
    public List<ProjectDto> Projects { get; set; } = new();
    public List<CertificationDto> Certifications { get; set; } = new();
}

public class SyncStatusDto
{
    public DateTime? LastSyncedAt { get; set; }
    public bool HasProfile { get; set; }
}

public class ExperienceDto
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}

public class EducationDto
{
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    public string StartYear { get; set; } = string.Empty;
    public string EndYear { get; set; } = string.Empty;
}

public class SkillDto
{
    public string Name { get; set; } = string.Empty;
    public int EndorsementCount { get; set; }
}

public class ProjectDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

public class CertificationDto
{
    public string Name { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string CredentialUrl { get; set; } = string.Empty;
}
