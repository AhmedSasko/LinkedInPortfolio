namespace LinkedInPortfolio.API.Models;

public class ProfileSnapshot
{
    public int Id { get; set; }
    public DateTime FetchedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string About { get; set; } = string.Empty;
    public string PhotoBase64 { get; set; } = string.Empty;

    public List<Experience> Experiences { get; set; } = new();
    public List<Education> Educations { get; set; } = new();
    public List<Skill> Skills { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public List<Certification> Certifications { get; set; } = new();
}
