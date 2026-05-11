using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.Models;
using LinkedInPortfolio.API.Services;
using Microsoft.EntityFrameworkCore;

namespace LinkedInPortfolio.Tests;

public class ProfileServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetLatest_WhenNoData_ReturnsNull()
    {
        using var context = CreateContext();
        var service = new ProfileService(context);
        var result = await service.GetLatestAsync(userId: 1);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatest_WhenDataExists_ReturnsLatestForUser()
    {
        using var context = CreateContext();
        context.ProfileSnapshots.Add(new ProfileSnapshot
        {
            UserId = 1,
            FetchedAt = DateTime.UtcNow,
            Name = "Ahmed Omran",
            Headline = "Software Engineer",
            Location = "Riyadh",
            About = "About me",
            PhotoBase64 = "abc123",
            Experiences = new List<Experience>
            {
                new() { Title = "Dev", Company = "Tuwaiq", StartDate = "2023", IsCurrent = true }
            },
            Skills = new List<Skill>
            {
                new() { Name = "C#", EndorsementCount = 10 }
            }
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context);
        var result = await service.GetLatestAsync(userId: 1);

        Assert.NotNull(result);
        Assert.Equal("Ahmed Omran", result.Name);
        Assert.Equal("Software Engineer", result.Headline);
        Assert.Single(result.Experiences);
        Assert.Equal("Dev", result.Experiences[0].Title);
        Assert.Single(result.Skills);
        Assert.Equal("C#", result.Skills[0].Name);
    }

    [Fact]
    public async Task GetLatest_ReturnsOnlySnapshotsForRequestedUser()
    {
        using var context = CreateContext();
        context.ProfileSnapshots.Add(new ProfileSnapshot
        {
            UserId = 2,
            FetchedAt = DateTime.UtcNow,
            Name = "Other User"
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context);
        var result = await service.GetLatestAsync(userId: 1);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStatus_WhenNoData_ReturnsNullTimestampAndHasProfileFalse()
    {
        using var context = CreateContext();
        var service = new ProfileService(context);
        var result = await service.GetStatusAsync(userId: 1);
        Assert.Null(result.LastSyncedAt);
        Assert.False(result.HasProfile);
    }

    [Fact]
    public async Task GetStatus_WhenDataExists_ReturnsCorrectStatus()
    {
        using var context = CreateContext();
        var fetchedAt = DateTime.UtcNow;
        context.ProfileSnapshots.Add(new ProfileSnapshot
        {
            UserId = 1,
            FetchedAt = fetchedAt,
            Name = "Ahmed"
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context);
        var result = await service.GetStatusAsync(userId: 1);

        Assert.Equal(fetchedAt, result.LastSyncedAt);
        Assert.True(result.HasProfile);
    }

    [Fact]
    public async Task SaveProfile_AppendsSnapshot_DoesNotDeleteExisting()
    {
        using var context = CreateContext();
        context.ProfileSnapshots.Add(new ProfileSnapshot
        {
            UserId = 1,
            Name = "Old Name",
            FetchedAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context);
        await service.SaveProfileAsync(userId: 1, new ProfileData
        {
            Name = "Ahmed Omran",
            Headline = "Engineer",
            FetchedAt = DateTime.UtcNow,
            Experiences = new List<ExperienceData>
            {
                new() { Title = "Dev", Company = "Tuwaiq", IsCurrent = true }
            }
        });

        // Should have 2 snapshots now (appended, not replaced)
        Assert.Equal(2, await context.ProfileSnapshots.CountAsync());
        var latest = await context.ProfileSnapshots
            .Include(p => p.Experiences)
            .OrderByDescending(p => p.FetchedAt)
            .FirstAsync();
        Assert.Equal("Ahmed Omran", latest.Name);
        Assert.Single(latest.Experiences);
    }

    [Fact]
    public async Task GetById_WithWrongUserId_ReturnsNull()
    {
        using var context = CreateContext();
        context.ProfileSnapshots.Add(new ProfileSnapshot
        {
            UserId = 1,
            FetchedAt = DateTime.UtcNow,
            Name = "Ahmed"
        });
        await context.SaveChangesAsync();

        var snapshot = await context.ProfileSnapshots.FirstAsync();
        var service = new ProfileService(context);

        // User 2 should NOT be able to access user 1's snapshot
        var result = await service.GetByIdAsync(userId: 2, snapshotId: snapshot.Id);
        Assert.Null(result);
    }
}
