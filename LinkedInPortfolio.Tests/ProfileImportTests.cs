using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace LinkedInPortfolio.Tests;

public class ProfileImportTests : IClassFixture<ProfileImportWebAppFactory>
{
    // Must match appsettings.json Jwt config (used by JWT middleware)
    private const string TestJwtKey = "dev-secret-key-must-be-at-least-32-characters-long!!";
    private const string TestIssuer = "LinkedInPortfolio";
    private const string TestAudience = "LinkedInPortfolio";

    private readonly ProfileImportWebAppFactory _factory;
    private readonly HttpClient _client;

    public ProfileImportTests(ProfileImportWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string GenerateTestJwt(int userId = 1)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, $"user{userId}@test.com"),
            new Claim("isAdmin", "false")
        };
        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Import_WithValidUrl_Returns200()
    {
        // Arrange: mock scraper returns valid ProfileData
        _factory.ScraperMock
            .Setup(s => s.ScrapeProfileAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProfileData
            {
                FetchedAt = DateTime.UtcNow,
                Name = "Test User",
                Headline = "Software Engineer",
                Location = "Riyadh",
                About = "About",
                Experiences = new List<ExperienceData>(),
                Educations = new List<EducationData>(),
                Skills = new List<SkillData>(),
                Projects = new List<ProjectData>(),
                Certifications = new List<CertificationData>()
            });

        // Register a user first so the user ID exists in the DB
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "importuser@example.com",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Get the actual user ID from the DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.First(u => u.Email == "importuser@example.com");

        var jwt = GenerateTestJwt(user.Id);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/profile/import")
        {
            Content = JsonContent.Create(new { linkedInUrl = "https://www.linkedin.com/in/testuser" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Import_WithInvalidUrl_Returns400()
    {
        // Register and get JWT
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "invalidurl@example.com",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.First(u => u.Email == "invalidurl@example.com");

        var jwt = GenerateTestJwt(user.Id);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/profile/import")
        {
            Content = JsonContent.Create(new { linkedInUrl = "https://www.example.com/not-linkedin" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_WithPrivateProfile_Returns400()
    {
        // Arrange: mock scraper throws InvalidOperationException
        _factory.ScraperMock
            .Setup(s => s.ScrapeProfileAsync(It.Is<string>(u => u.Contains("private"))))
            .ThrowsAsync(new InvalidOperationException("This profile is private."));

        // Register a user
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "privateprofile@example.com",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.First(u => u.Email == "privateprofile@example.com");

        var jwt = GenerateTestJwt(user.Id);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/profile/import")
        {
            Content = JsonContent.Create(new { linkedInUrl = "https://www.linkedin.com/in/private-user" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_WithoutAuth_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/profile/import")
        {
            Content = JsonContent.Create(new { linkedInUrl = "https://www.linkedin.com/in/testuser" })
        };
        // No Authorization header

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// Custom WebApplicationFactory for profile import tests.
/// Replaces MySQL with SQLite in-memory and mocks ILinkedInScraperService.
/// </summary>
public class ProfileImportWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    public Mock<ILinkedInScraperService> ScraperMock { get; } = new();

    public ProfileImportWebAppFactory()
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

            // Replace ILinkedInScraperService with mock
            var scraperDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ILinkedInScraperService));
            if (scraperDescriptor != null)
                services.Remove(scraperDescriptor);

            services.AddScoped<ILinkedInScraperService>(_ => ScraperMock.Object);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
