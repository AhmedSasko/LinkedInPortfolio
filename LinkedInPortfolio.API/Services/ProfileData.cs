namespace LinkedInPortfolio.API.Services;

public class ProfileData
{
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string About { get; set; } = string.Empty;
    public string PhotoBase64 { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public List<ExperienceData> Experiences { get; set; } = new();
    public List<EducationData> Educations { get; set; } = new();
    public List<SkillData> Skills { get; set; } = new();
    public List<ProjectData> Projects { get; set; } = new();
    public List<CertificationData> Certifications { get; set; } = new();
}

public class ExperienceData
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}

public class EducationData
{
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    public string StartYear { get; set; } = string.Empty;
    public string EndYear { get; set; } = string.Empty;
}

public class SkillData
{
    public string Name { get; set; } = string.Empty;
    public int EndorsementCount { get; set; }
}

public class ProjectData
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

public class CertificationData
{
    public string Name { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string CredentialUrl { get; set; } = string.Empty;
}
