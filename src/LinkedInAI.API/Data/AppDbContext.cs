using LinkedInAI.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInAI.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LinkedInAccount> LinkedInAccounts => Set<LinkedInAccount>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ProfileExperience> ProfileExperiences => Set<ProfileExperience>();
    public DbSet<ProfileEducation> ProfileEducations => Set<ProfileEducation>();
    public DbSet<ProfileSkill> ProfileSkills => Set<ProfileSkill>();
    public DbSet<ProfileCertification> ProfileCertifications => Set<ProfileCertification>();
    public DbSet<ProfileProject> ProfileProjects => Set<ProfileProject>();
    public DbSet<ProfileLanguage> ProfileLanguages => Set<ProfileLanguage>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<AnalysisResult> AnalysisResults => Set<AnalysisResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(255);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(rt => rt.Token).IsUnique();
            e.Property(rt => rt.Token).HasMaxLength(512);
            e.HasOne(rt => rt.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(rt => rt.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // LinkedInAccount
        modelBuilder.Entity<LinkedInAccount>(e =>
        {
            e.HasIndex(la => la.UserId).IsUnique();
            e.Property(la => la.ProfileJson).HasColumnType("json");
            e.HasOne(la => la.User)
             .WithOne(u => u.LinkedInAccount)
             .HasForeignKey<LinkedInAccount>(la => la.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Profile
        modelBuilder.Entity<Profile>(e =>
        {
            e.Property(p => p.About).HasColumnType("longtext");
            e.Property(p => p.PhotoBase64).HasColumnType("longtext");
            e.HasOne(p => p.User)
             .WithMany(u => u.Profiles)
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Profile children — typed configurations that link inverse nav properties
        // so EF Core uses ProfileId (not a shadow ProfileId1) for Include queries.
        modelBuilder.Entity<ProfileExperience>()
            .HasOne(e => e.Profile)
            .WithMany(p => p.Experiences)
            .HasForeignKey(e => e.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileEducation>()
            .HasOne(e => e.Profile)
            .WithMany(p => p.Educations)
            .HasForeignKey(e => e.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileSkill>()
            .HasOne(s => s.Profile)
            .WithMany(p => p.Skills)
            .HasForeignKey(s => s.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileCertification>()
            .HasOne(c => c.Profile)
            .WithMany(p => p.Certifications)
            .HasForeignKey(c => c.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileProject>()
            .HasOne(pr => pr.Profile)
            .WithMany(p => p.Projects)
            .HasForeignKey(pr => pr.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileLanguage>()
            .HasOne(l => l.Profile)
            .WithMany(p => p.Languages)
            .HasForeignKey(l => l.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfileExperience>(e =>
            e.Property(x => x.Description).HasColumnType("longtext"));

        modelBuilder.Entity<ProfileProject>(e =>
            e.Property(x => x.Description).HasColumnType("longtext"));

        // Resume
        modelBuilder.Entity<Resume>(e =>
        {
            e.Property(r => r.ParsedData).HasColumnType("json");
            e.HasOne(r => r.User)
             .WithMany(u => u.Resumes)
             .HasForeignKey(r => r.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // AnalysisResult
        modelBuilder.Entity<AnalysisResult>(e =>
        {
            e.Property(a => a.Result).HasColumnType("json");
            e.HasIndex(a => a.MessageId).IsUnique();
            e.HasOne(a => a.User)
             .WithMany(u => u.AnalysisResults)
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Profile)
             .WithMany(p => p.AnalysisResults)
             .HasForeignKey(a => a.ProfileId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
