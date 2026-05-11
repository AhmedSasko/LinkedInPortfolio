using LinkedInPortfolio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInPortfolio.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ProfileSnapshot> ProfileSnapshots => Set<ProfileSnapshot>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Education> Educations => Set<Education>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfileSnapshot>(e =>
        {
            e.Property(p => p.PhotoBase64).HasColumnType("longtext");
            e.Property(p => p.About).HasColumnType("longtext");
            e.HasMany(p => p.Experiences).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Educations).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Skills).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Projects).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Certifications).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Experience>().Property(e => e.Description).HasColumnType("longtext");
        modelBuilder.Entity<Project>().Property(p => p.Description).HasColumnType("longtext");

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.Property(s => s.Value).HasColumnType("longtext");
        });
    }
}
