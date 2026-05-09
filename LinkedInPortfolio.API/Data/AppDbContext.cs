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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfileSnapshot>(e =>
        {
            e.Property(p => p.PhotoBase64).HasColumnType("nvarchar(max)");
            e.Property(p => p.About).HasColumnType("nvarchar(max)");
            e.HasMany(p => p.Experiences).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Educations).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Skills).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Projects).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Certifications).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Experience>().Property(e => e.Description).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<Project>().Property(p => p.Description).HasColumnType("nvarchar(max)");
    }
}
