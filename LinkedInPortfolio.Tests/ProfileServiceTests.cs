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
    public async Task GetProfile_WhenNoData_ReturnsNull()
    {
        using var context = CreateContext();
        var service = new ProfileService(context);
        var result = await service.GetProfileAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProfile_WhenDataExists_ReturnsMappedDto()
    {
        using var context = CreateContext();
        context.ProfileSnapshots.Add(new ProfileSnapshot
        {
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
        var result = await service.GetProfileAsync();

        Assert.NotNull(result);
        Assert.Equal("Ahmed Omran", result.Name);
        Assert.Equal("Software Engineer", result.Headline);
        Assert.Single(result.Experience);
        Assert.Equal("Dev", result.Experience[0].Title);
        Assert.Single(result.Skills);
        Assert.Equal("C#", result.Skills[0].Name);
    }

    [Fact]
    public async Task GetStatus_WhenNoData_ReturnsNullTimestampAndZeroCounts()
    {
        using var context = CreateContext();
        var service = new ProfileService(context);
        var result = await service.GetStatusAsync();
        Assert.Null(result.LastSyncedAt);
        Assert.Equal(0, result.ExperienceCount);
        Assert.Equal(0, result.SkillCount);
    }

    [Fact]
    public async Task GetStatus_WhenDataExists_ReturnsCorrectCounts()
    {
        using var context = CreateContext();
        var fetchedAt = DateTime.UtcNow;
        context.ProfileSnapshots.Add(new ProfileSnapshot
        {
            FetchedAt = fetchedAt,
            Name = "Ahmed",
            Experiences = new List<Experience>
            {
                new() { Title = "Dev", Company = "A" },
                new() { Title = "Lead", Company = "B" }
            },
            Skills = new List<Skill> { new() { Name = "C#" } }
        });
        await context.SaveChangesAsync();

        var service = new ProfileService(context);
        var result = await service.GetStatusAsync();

        Assert.Equal(fetchedAt, result.LastSyncedAt);
        Assert.Equal(2, result.ExperienceCount);
        Assert.Equal(1, result.SkillCount);
    }

    [Fact]
    public async Task SaveProfile_WhenExistingData_ReplacesIt()
    {
        using var context = CreateContext();
        context.ProfileSnapshots.Add(new ProfileSnapshot { Name = "Old Name", FetchedAt = DateTime.UtcNow.AddDays(-1) });
        await context.SaveChangesAsync();

        var service = new ProfileService(context);
        await service.SaveProfileAsync(new ProfileData
        {
            Name = "Ahmed Omran",
            Headline = "Engineer",
            FetchedAt = DateTime.UtcNow,
            Experiences = new List<ExperienceData>
            {
                new() { Title = "Dev", Company = "Tuwaiq", IsCurrent = true }
            }
        });

        Assert.Equal(1, await context.ProfileSnapshots.CountAsync());
        var saved = await context.ProfileSnapshots.Include(p => p.Experiences).FirstAsync();
        Assert.Equal("Ahmed Omran", saved.Name);
        Assert.Single(saved.Experiences);
    }
}
