namespace LinkedInAI.API.Services;

public class ProfileData
{
    public string? Name { get; set; }
    public string? Headline { get; set; }
    public string? Location { get; set; }
    public string? About { get; set; }
    public string? PhotoUrl { get; set; }
    public string? PhotoBase64 { get; set; }
    public List<ExperienceData> Experiences { get; set; } = [];
    public List<EducationData> Educations { get; set; } = [];
    public List<SkillData> Skills { get; set; } = [];
    public List<CertificationData> Certifications { get; set; } = [];
    public List<ProjectData> Projects { get; set; } = [];
}

public record ExperienceData(string? Title, string? Company, string? StartDate, string? EndDate, string? Description, bool IsCurrent);
public record EducationData(string? School, string? Degree, string? FieldOfStudy, string? StartYear, string? EndYear);
public record SkillData(string Name, int EndorsementCount = 0);
public record CertificationData(string? Name, string? IssuingOrganization, string? IssueDate, string? CredentialUrl);
public record ProjectData(string? Title, string? Description, string? Url, string? StartDate, string? EndDate);
