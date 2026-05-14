namespace LinkedInAI.API.Models;

public class Profile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Name { get; set; }
    public string? Headline { get; set; }
    public string? Location { get; set; }
    public string? About { get; set; }
    public string? PhotoUrl { get; set; }
    public string? PhotoBase64 { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public List<ProfileExperience> Experiences { get; set; } = [];
    public List<ProfileEducation> Educations { get; set; } = [];
    public List<ProfileSkill> Skills { get; set; } = [];
    public List<ProfileCertification> Certifications { get; set; } = [];
    public List<ProfileProject> Projects { get; set; } = [];
    public List<ProfileLanguage> Languages { get; set; } = [];
    public List<AnalysisResult> AnalysisResults { get; set; } = [];
}

public class ProfileExperience
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Description { get; set; }
    public bool IsCurrent { get; set; }

    public Profile Profile { get; set; } = null!;
}

public class ProfileEducation
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public string? School { get; set; }
    public string? Degree { get; set; }
    public string? FieldOfStudy { get; set; }
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }

    public Profile Profile { get; set; } = null!;
}

public class ProfileSkill
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public required string Name { get; set; }
    public int EndorsementCount { get; set; }

    public Profile Profile { get; set; } = null!;
}

public class ProfileCertification
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public string? Name { get; set; }
    public string? IssuingOrganization { get; set; }
    public string? IssueDate { get; set; }
    public string? CredentialUrl { get; set; }

    public Profile Profile { get; set; } = null!;
}

public class ProfileProject
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }

    public Profile Profile { get; set; } = null!;
}

public class ProfileLanguage
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public required string Name { get; set; }
    public string? Proficiency { get; set; }

    public Profile Profile { get; set; } = null!;
}
