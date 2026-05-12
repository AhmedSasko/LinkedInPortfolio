using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinkedInPortfolio.Tests;

public class ProfileEditTests(ProfileEditWebAppFactory factory) : IClassFixture<ProfileEditWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private void AuthorizeAs(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task UpdateProfile_WithoutAuth_Returns401()
    {
        var res = await _client.PutAsJsonAsync("/api/profile", new UpdateProfileRequest { Name = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithAuth_Returns200()
    {
        // Register and login first
        var regRes = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("edit@test.com", "Password123!"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        AuthorizeAs(auth!.Token);

        var req = new UpdateProfileRequest
        {
            Name = "Test User",
            Headline = "Engineer",
            Location = "Riyadh",
            About = "About me",
            Experiences = new() { new ExperienceDto { Title = "Dev", Company = "Acme", StartDate = "2020", EndDate = "", Description = "", IsCurrent = true } }
        };

        var res = await _client.PutAsJsonAsync("/api/profile", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var profile = await res.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Equal("Test User", profile!.Name);
        Assert.Single(profile.Experiences);
    }

    [Fact]
    public async Task UpdateProfile_TwiceReplaces_NotAppends()
    {
        var regRes = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("replace@test.com", "Password123!"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        AuthorizeAs(auth!.Token);

        // First save: 2 skills
        await _client.PutAsJsonAsync("/api/profile", new UpdateProfileRequest
        {
            Name = "User",
            Skills = new() { new SkillDto { Name = "C#" }, new SkillDto { Name = "Go" } }
        });

        // Second save: 1 skill — should replace, not append
        var res = await _client.PutAsJsonAsync("/api/profile", new UpdateProfileRequest
        {
            Name = "User",
            Skills = new() { new SkillDto { Name = "Rust" } }
        });

        var profile = await res.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Single(profile!.Skills);
        Assert.Equal("Rust", profile.Skills[0].Name);
    }

    [Fact]
    public async Task GetLatest_AfterUpdate_ReturnsUpdatedProfile()
    {
        var regRes = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("getlatest@test.com", "Password123!"));
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        AuthorizeAs(auth!.Token);

        await _client.PutAsJsonAsync("/api/profile", new UpdateProfileRequest { Name = "Latest User", Headline = "CTO" });

        var res = await _client.GetAsync("/api/profile/latest");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var profile = await res.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Equal("Latest User", profile!.Name);
        Assert.Equal("CTO", profile.Headline);
    }
}

/// <summary>
/// Custom WebApplicationFactory for profile edit tests.
/// Replaces MySQL with SQLite in-memory.
/// </summary>
public class ProfileEditWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public ProfileEditWebAppFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Remove ALL descriptors that mention AppDbContext to cleanly replace provider
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Remove IDbContextOptionsConfiguration<AppDbContext> which holds the MySQL setup
            var configType = typeof(IDbContextOptionsConfiguration<AppDbContext>);
            var configDescriptors = services.Where(d => d.ServiceType == configType).ToList();
            foreach (var d in configDescriptors)
                services.Remove(d);

            // Add SQLite in-memory DB
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
