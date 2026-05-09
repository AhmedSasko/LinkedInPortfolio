namespace LinkedInPortfolio.API.DTOs;

public class ProfileStatusDto
{
    public DateTime? LastSyncedAt { get; set; }
    public int ExperienceCount { get; set; }
    public int EducationCount { get; set; }
    public int SkillCount { get; set; }
    public int ProjectCount { get; set; }
    public int CertificationCount { get; set; }
}
