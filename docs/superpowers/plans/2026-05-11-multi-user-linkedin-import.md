# Multi-User LinkedIn Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add multi-user auth (email+password/JWT) and LinkedIn profile import by public URL, replacing the old single-user sync approach.

**Architecture:** JWT-based auth layered onto existing .NET 10 + React stack. Each User owns their ProfileSnapshots. Import calls headless Chromium against the user's public LinkedIn URL — no cookies, no login. Admin sees all users' profiles; regular users see only their own.

**Tech Stack:** ASP.NET Core 10, EF Core 9, Pomelo MySQL, PuppeteerSharp, Microsoft.AspNetCore.Authentication.JwtBearer, React + TanStack Query + React Router

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Delete | `LinkedInPortfolio.API/Models/AppSetting.cs` | Stale — no longer used |
| Delete | `LinkedInPortfolio.API/Services/ISettingsService.cs` | Stale |
| Delete | `LinkedInPortfolio.API/Services/SettingsService.cs` | Stale |
| Delete | `LinkedInPortfolio.API/Controllers/SettingsController.cs` | Stale |
| Delete | `LinkedInPortfolio.API/Controllers/SyncController.cs` | Replaced by ProfileController.Import |
| Delete | `LinkedInPortfolio.API/DTOs/SyncResultDto.cs` | No longer used |
| Delete | `LinkedInPortfolio.Tests/SyncControllerTests.cs` | SyncController is gone |
| Create | `LinkedInPortfolio.API/Models/User.cs` | New auth entity |
| Modify | `LinkedInPortfolio.API/Data/AppDbContext.cs` | Add Users DbSet, FK config, remove AppSetting |
| Modify | `LinkedInPortfolio.API/Models/ProfileSnapshot.cs` | Add UserId + User navigation |
| Modify | `LinkedInPortfolio.API/appsettings.json` | Add Jwt config section |
| Modify | `docker-compose.yml` | Add Jwt env vars |
| Modify | `LinkedInPortfolio.API/LinkedInPortfolio.API.csproj` | Add JwtBearer package |
| Create | `LinkedInPortfolio.API/DTOs/AuthDto.cs` | RegisterRequest, LoginRequest, AuthResponseDto |
| Create | `LinkedInPortfolio.API/Services/IAuthService.cs` | Register + Login interface |
| Create | `LinkedInPortfolio.API/Services/AuthService.cs` | Password hashing, JWT generation |
| Create | `LinkedInPortfolio.API/Controllers/AuthController.cs` | POST /api/auth/register, /api/auth/login |
| Modify | `LinkedInPortfolio.API/DTOs/ProfileDto.cs` | Add ImportRequest, UserEmail to ProfileSummaryDto |
| Modify | `LinkedInPortfolio.API/Services/IProfileService.cs` | Add userId params to all methods |
| Modify | `LinkedInPortfolio.API/Services/ProfileService.cs` | Scope all queries to userId |
| Modify | `LinkedInPortfolio.API/Controllers/ProfileController.cs` | [Authorize], Import endpoint, user-scoped methods |
| Modify | `LinkedInPortfolio.API/Services/ILinkedInScraperService.cs` | Change signature to accept URL |
| Modify | `LinkedInPortfolio.API/Services/LinkedInScraperService.cs` | Headless, public URL, no login loop |
| Modify | `LinkedInPortfolio.API/Dockerfile` | Add Chromium system libraries to runtime stage |
| Modify | `LinkedInPortfolio.API/Program.cs` | JWT auth middleware, register AuthService, remove SettingsService |
| Create | `LinkedInPortfolio.Tests/AuthControllerTests.cs` | Auth register/login tests |
| Create | `LinkedInPortfolio.Tests/ProfileImportTests.cs` | Import endpoint tests |
| Create | `linkedin-portfolio-ui/src/api/authApi.ts` | register() and login() API calls |
| Modify | `linkedin-portfolio-ui/src/api/profileApi.ts` | Add JWT interceptor + importProfile() |
| Modify | `linkedin-portfolio-ui/src/types/profile.ts` | Add AuthResponseDto, ImportRequest types |
| Create | `linkedin-portfolio-ui/src/hooks/useAuth.ts` | Decode JWT, expose user/isAdmin/logout |
| Create | `linkedin-portfolio-ui/src/components/ProtectedRoute.tsx` | Redirect to /login if no token |
| Create | `linkedin-portfolio-ui/src/pages/LoginPage.tsx` | Login form |
| Create | `linkedin-portfolio-ui/src/pages/RegisterPage.tsx` | Register form |
| Create | `linkedin-portfolio-ui/src/pages/ImportPage.tsx` | LinkedIn URL input + import button |
| Modify | `linkedin-portfolio-ui/src/App.tsx` | Add /login /register /import routes, wrap with ProtectedRoute |
| Modify | `linkedin-portfolio-ui/src/pages/PortfolioPage.tsx` | Update empty state link to /import |
| Modify | `linkedin-portfolio-ui/src/pages/AdminPage.tsx` | Remove sync card, show all-users list (admin only) |

---

## Task 1: Cleanup stale code + User model + migrations

**Files:**
- Delete: `LinkedInPortfolio.API/Models/AppSetting.cs`
- Delete: `LinkedInPortfolio.API/Services/ISettingsService.cs`
- Delete: `LinkedInPortfolio.API/Services/SettingsService.cs`
- Delete: `LinkedInPortfolio.API/Controllers/SettingsController.cs`
- Delete: `LinkedInPortfolio.API/Controllers/SyncController.cs`
- Delete: `LinkedInPortfolio.API/DTOs/SyncResultDto.cs`
- Delete: `LinkedInPortfolio.Tests/SyncControllerTests.cs`
- Create: `LinkedInPortfolio.API/Models/User.cs`
- Modify: `LinkedInPortfolio.API/Data/AppDbContext.cs`
- Modify: `LinkedInPortfolio.API/Program.cs`

- [ ] **Step 1: Delete stale files**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
rm LinkedInPortfolio.API/Models/AppSetting.cs
rm LinkedInPortfolio.API/Services/ISettingsService.cs
rm LinkedInPortfolio.API/Services/SettingsService.cs
rm LinkedInPortfolio.API/Controllers/SettingsController.cs
rm LinkedInPortfolio.API/Controllers/SyncController.cs
rm LinkedInPortfolio.API/DTOs/SyncResultDto.cs
rm LinkedInPortfolio.Tests/SyncControllerTests.cs
```

- [ ] **Step 2: Create `LinkedInPortfolio.API/Models/User.cs`**

```csharp
namespace LinkedInPortfolio.API.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ProfileSnapshot> ProfileSnapshots { get; set; } = new();
}
```

- [ ] **Step 3: Add UserId + navigation property to `LinkedInPortfolio.API/Models/ProfileSnapshot.cs`**

Replace the full file content:

```csharp
namespace LinkedInPortfolio.API.Models;

public class ProfileSnapshot
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
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
```

- [ ] **Step 4: Update `LinkedInPortfolio.API/Data/AppDbContext.cs`**

Replace the full file content:

```csharp
using LinkedInPortfolio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInPortfolio.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ProfileSnapshot> ProfileSnapshots => Set<ProfileSnapshot>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Education> Educations => Set<Education>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Certification> Certifications => Set<Certification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.Email).HasMaxLength(255);
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<ProfileSnapshot>(e =>
        {
            e.Property(p => p.PhotoBase64).HasColumnType("longtext");
            e.Property(p => p.About).HasColumnType("longtext");
            e.HasOne(p => p.User)
                .WithMany(u => u.ProfileSnapshots)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Experiences).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Educations).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Skills).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Projects).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Certifications).WithOne().HasForeignKey(x => x.ProfileSnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Experience>().Property(e => e.Description).HasColumnType("longtext");
        modelBuilder.Entity<Project>().Property(p => p.Description).HasColumnType("longtext");
    }
}
```

- [ ] **Step 5: Update `LinkedInPortfolio.API/Program.cs`** — remove SettingsService registration

Replace full file content:

```csharp
using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ILinkedInScraperService, LinkedInScraperService>();
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173",
                "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReact");
app.MapControllers();
app.Run();
```

- [ ] **Step 6: Generate migration**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio/LinkedInPortfolio.API
dotnet tool restore
dotnet ef migrations add AddUsersAndRefactorSnapshots --output-dir Data/Migrations
```

Expected: New migration file with `CreateTable("Users", ...)`, `AddColumn("UserId", ...)` on ProfileSnapshots, `DropTable("AppSettings")`.

- [ ] **Step 7: Build to verify**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
git add -A
git commit -m "feat: remove stale code, add User model and migration"
```

---

## Task 2: JWT auth — AuthService + AuthController + Program.cs

**Files:**
- Modify: `LinkedInPortfolio.API/LinkedInPortfolio.API.csproj`
- Modify: `LinkedInPortfolio.API/appsettings.json`
- Modify: `docker-compose.yml`
- Create: `LinkedInPortfolio.API/DTOs/AuthDto.cs`
- Create: `LinkedInPortfolio.API/Services/IAuthService.cs`
- Create: `LinkedInPortfolio.API/Services/AuthService.cs`
- Create: `LinkedInPortfolio.API/Controllers/AuthController.cs`
- Modify: `LinkedInPortfolio.API/Program.cs`

- [ ] **Step 1: Add JwtBearer package**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio/LinkedInPortfolio.API
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

Expected: Package added successfully.

- [ ] **Step 2: Update `LinkedInPortfolio.API/appsettings.json`**

Replace the full file content:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3307;User=root;Password=Tuwaiq@123;Database=LinkedIn;AllowUserVariables=True;CharSet=utf8mb4"
  },
  "Jwt": {
    "Key": "dev-secret-key-must-be-at-least-32-characters-long!!",
    "Issuer": "LinkedInPortfolio",
    "Audience": "LinkedInPortfolio",
    "ExpiryDays": "7"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 3: Update `docker-compose.yml`** — add JWT env vars to api service

Read the file first, then add under `environment:`:

```yaml
services:
  api:
    build:
      context: .
      dockerfile: LinkedInPortfolio.API/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=host.docker.internal;Port=3307;User=root;Password=Tuwaiq@123;Database=LinkedIn;AllowUserVariables=True;CharSet=utf8mb4"
      Jwt__Key: "dev-secret-key-must-be-at-least-32-characters-long!!"
      Jwt__Issuer: "LinkedInPortfolio"
      Jwt__Audience: "LinkedInPortfolio"
      Jwt__ExpiryDays: "7"
    extra_hosts:
      - "host.docker.internal:host-gateway"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/api/profile/status"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    networks:
      - linkedin-net

  frontend:
    build:
      context: ./linkedin-portfolio-ui
      dockerfile: Dockerfile
    ports:
      - "3000:80"
    depends_on:
      api:
        condition: service_healthy
    networks:
      - linkedin-net

networks:
  linkedin-net:
    driver: bridge
```

- [ ] **Step 4: Create `LinkedInPortfolio.API/DTOs/AuthDto.cs`**

```csharp
namespace LinkedInPortfolio.API.DTOs;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
}
```

- [ ] **Step 5: Create `LinkedInPortfolio.API/Services/IAuthService.cs`**

```csharp
using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(string email, string password);
    Task<AuthResponseDto?> LoginAsync(string email, string password);
}
```

- [ ] **Step 6: Create `LinkedInPortfolio.API/Services/AuthService.cs`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LinkedInPortfolio.API.Services;

public class AuthService(AppDbContext db, IConfiguration config) : IAuthService
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<AuthResponseDto?> RegisterAsync(string email, string password)
    {
        var normalised = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == normalised))
            return null;

        var isFirst = !await db.Users.AnyAsync();
        var user = new User
        {
            Email = normalised,
            IsAdmin = isFirst,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return new AuthResponseDto { Token = GenerateToken(user) };
    }

    public async Task<AuthResponseDto?> LoginAsync(string email, string password)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalised);
        if (user is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;

        return new AuthResponseDto { Token = GenerateToken(user) };
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("isAdmin", user.IsAdmin.ToString().ToLower())
        };
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(int.Parse(config["Jwt:ExpiryDays"]!)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 7: Create `LinkedInPortfolio.API/Controllers/AuthController.cs`**

```csharp
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInPortfolio.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        if (request.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        var result = await authService.RegisterAsync(request.Email, request.Password);
        if (result is null)
            return BadRequest(new { message = "An account with this email already exists." });

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        var result = await authService.LoginAsync(request.Email, request.Password);
        if (result is null)
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(result);
    }
}
```

- [ ] **Step 8: Update `LinkedInPortfolio.API/Program.cs`** — add JWT auth and register AuthService

Replace full file content:

```csharp
using System.Text;
using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ILinkedInScraperService, LinkedInScraperService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpClient();

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("isAdmin", "true"));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173",
                "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

- [ ] **Step 9: Build to verify**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio/LinkedInPortfolio.API
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 10: Commit**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
git add -A
git commit -m "feat: add JWT auth — AuthService, AuthController, Program.cs middleware"
```

---

## Task 3: LinkedInScraperService — headless + public URL

**Files:**
- Modify: `LinkedInPortfolio.API/Services/ILinkedInScraperService.cs`
- Modify: `LinkedInPortfolio.API/Services/LinkedInScraperService.cs`

> **Note:** This task MUST run before Task 4. Task 4's ProfileController calls `ScrapeProfileAsync(string url)` — the new signature defined here.

- [ ] **Step 1: Update `LinkedInPortfolio.API/Services/ILinkedInScraperService.cs`**

```csharp
namespace LinkedInPortfolio.API.Services;

public interface ILinkedInScraperService
{
    Task<ProfileData> ScrapeProfileAsync(string profileUrl);
}
```

- [ ] **Step 2: Replace `LinkedInPortfolio.API/Services/LinkedInScraperService.cs`**

(Full replacement — see the scraper code block in what was previously labelled Task 4 below.)

Copy the full `LinkedInScraperService` implementation from the section titled **"Task 4: LinkedInScraperService — headless + public URL"** below.

- [ ] **Step 3: Build to verify**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio/LinkedInPortfolio.API
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
git add LinkedInPortfolio.API/Services/ILinkedInScraperService.cs \
        LinkedInPortfolio.API/Services/LinkedInScraperService.cs
git commit -m "feat: rewrite scraper for headless public URL (no login/cookies)"
```

---

## Task 4: ProfileService user-scoping + ProfileController update

**Files:**
- Modify: `LinkedInPortfolio.API/DTOs/ProfileDto.cs`
- Modify: `LinkedInPortfolio.API/Services/IProfileService.cs`
- Modify: `LinkedInPortfolio.API/Services/ProfileService.cs`
- Modify: `LinkedInPortfolio.API/Controllers/ProfileController.cs`

- [ ] **Step 1: Update `LinkedInPortfolio.API/DTOs/ProfileDto.cs`** — add `UserEmail` to summary, add `ImportRequest`

Replace full file content:

```csharp
namespace LinkedInPortfolio.API.DTOs;

public class ProfileSummaryDto
{
    public int Id { get; set; }
    public DateTime FetchedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int ExperienceCount { get; set; }
    public int EducationCount { get; set; }
    public int SkillCount { get; set; }
    public int ProjectCount { get; set; }
    public int CertificationCount { get; set; }
}

public class ProfileDto
{
    public DateTime FetchedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string About { get; set; } = string.Empty;
    public string PhotoBase64 { get; set; } = string.Empty;
    public List<ExperienceDto> Experience { get; set; } = new();
    public List<EducationDto> Education { get; set; } = new();
    public List<SkillDto> Skills { get; set; } = new();
    public List<ProjectDto> Projects { get; set; } = new();
    public List<CertificationDto> Certifications { get; set; } = new();
}

public class ExperienceDto
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}

public class EducationDto
{
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    public string StartYear { get; set; } = string.Empty;
    public string EndYear { get; set; } = string.Empty;
}

public class SkillDto
{
    public string Name { get; set; } = string.Empty;
    public int EndorsementCount { get; set; }
}

public class ProjectDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

public class CertificationDto
{
    public string Name { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string CredentialUrl { get; set; } = string.Empty;
}

public record ImportRequest(string LinkedInUrl);
```

- [ ] **Step 2: Update `LinkedInPortfolio.API/Services/IProfileService.cs`**

Replace full file content:

```csharp
using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public interface IProfileService
{
    Task<List<ProfileSummaryDto>> GetAllSummariesAsync();
    Task<ProfileDto?> GetProfileAsync(int userId);
    Task<ProfileDto?> GetProfileByIdAsync(int id, int userId, bool isAdmin);
    Task<ProfileStatusDto> GetStatusAsync(int userId);
    Task SaveProfileAsync(ProfileData data, int userId);
}
```

- [ ] **Step 3: Update `LinkedInPortfolio.API/Services/ProfileService.cs`**

Replace full file content:

```csharp
using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInPortfolio.API.Services;

public class ProfileService(AppDbContext db) : IProfileService
{
    public async Task<List<ProfileSummaryDto>> GetAllSummariesAsync()
    {
        var snapshots = await db.ProfileSnapshots
            .Include(p => p.User)
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .OrderByDescending(p => p.FetchedAt)
            .ToListAsync();

        return snapshots.Select(p => new ProfileSummaryDto
        {
            Id = p.Id,
            FetchedAt = p.FetchedAt,
            Name = p.Name,
            Headline = p.Headline,
            UserEmail = p.User?.Email ?? string.Empty,
            ExperienceCount = p.Experiences.Count,
            EducationCount = p.Educations.Count,
            SkillCount = p.Skills.Count,
            ProjectCount = p.Projects.Count,
            CertificationCount = p.Certifications.Count
        }).ToList();
    }

    public async Task<ProfileDto?> GetProfileAsync(int userId)
    {
        var snapshot = await db.ProfileSnapshots
            .Where(p => p.UserId == userId)
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync();

        return snapshot is null ? null : MapToDto(snapshot);
    }

    public async Task<ProfileDto?> GetProfileByIdAsync(int id, int userId, bool isAdmin)
    {
        var snapshot = await db.ProfileSnapshots
            .Where(p => p.Id == id && (isAdmin || p.UserId == userId))
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .FirstOrDefaultAsync();

        return snapshot is null ? null : MapToDto(snapshot);
    }

    public async Task<ProfileStatusDto> GetStatusAsync(int userId)
    {
        var snapshot = await db.ProfileSnapshots
            .Where(p => p.UserId == userId)
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync();

        if (snapshot is null)
            return new ProfileStatusDto();

        return new ProfileStatusDto
        {
            LastSyncedAt = snapshot.FetchedAt,
            ExperienceCount = snapshot.Experiences.Count,
            EducationCount = snapshot.Educations.Count,
            SkillCount = snapshot.Skills.Count,
            ProjectCount = snapshot.Projects.Count,
            CertificationCount = snapshot.Certifications.Count
        };
    }

    public async Task SaveProfileAsync(ProfileData data, int userId)
    {
        var snapshot = new ProfileSnapshot
        {
            FetchedAt = data.FetchedAt,
            UserId = userId,
            Name = data.Name,
            Headline = data.Headline,
            Location = data.Location,
            About = data.About,
            PhotoBase64 = data.PhotoBase64,
            Experiences = data.Experiences.Select(e => new Experience
            {
                Title = e.Title,
                Company = e.Company,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Description = e.Description,
                IsCurrent = e.IsCurrent
            }).ToList(),
            Educations = data.Educations.Select(e => new Education
            {
                School = e.School,
                Degree = e.Degree,
                FieldOfStudy = e.FieldOfStudy,
                StartYear = e.StartYear,
                EndYear = e.EndYear
            }).ToList(),
            Skills = data.Skills.Select(s => new Skill
            {
                Name = s.Name,
                EndorsementCount = s.EndorsementCount
            }).ToList(),
            Projects = data.Projects.Select(p => new Project
            {
                Title = p.Title,
                Description = p.Description,
                Url = p.Url,
                StartDate = p.StartDate,
                EndDate = p.EndDate
            }).ToList(),
            Certifications = data.Certifications.Select(c => new Certification
            {
                Name = c.Name,
                IssuingOrganization = c.IssuingOrganization,
                IssueDate = c.IssueDate,
                CredentialUrl = c.CredentialUrl
            }).ToList()
        };

        db.ProfileSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
    }

    private static ProfileDto MapToDto(ProfileSnapshot s) => new()
    {
        FetchedAt = s.FetchedAt,
        Name = s.Name,
        Headline = s.Headline,
        Location = s.Location,
        About = s.About,
        PhotoBase64 = s.PhotoBase64,
        Experience = s.Experiences.Select(e => new ExperienceDto { Title = e.Title, Company = e.Company, StartDate = e.StartDate, EndDate = e.EndDate, Description = e.Description, IsCurrent = e.IsCurrent }).ToList(),
        Education = s.Educations.Select(e => new EducationDto { School = e.School, Degree = e.Degree, FieldOfStudy = e.FieldOfStudy, StartYear = e.StartYear, EndYear = e.EndYear }).ToList(),
        Skills = s.Skills.Select(sk => new SkillDto { Name = sk.Name, EndorsementCount = sk.EndorsementCount }).ToList(),
        Projects = s.Projects.Select(p => new ProjectDto { Title = p.Title, Description = p.Description, Url = p.Url, StartDate = p.StartDate, EndDate = p.EndDate }).ToList(),
        Certifications = s.Certifications.Select(c => new CertificationDto { Name = c.Name, IssuingOrganization = c.IssuingOrganization, IssueDate = c.IssueDate, CredentialUrl = c.CredentialUrl }).ToList()
    };
}
```

- [ ] **Step 4: Replace `LinkedInPortfolio.API/Controllers/ProfileController.cs`**

```csharp
using System.Security.Claims;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInPortfolio.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController(
    IProfileService profileService,
    ILinkedInScraperService scraperService) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.FindFirstValue("isAdmin") == "true";

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LinkedInUrl) ||
            !request.LinkedInUrl.Contains("linkedin.com/in/"))
            return BadRequest(new { message = "Please provide a valid LinkedIn profile URL (e.g. https://www.linkedin.com/in/your-username)." });

        try
        {
            var data = await scraperService.ScrapeProfileAsync(request.LinkedInUrl);
            await profileService.SaveProfileAsync(data, CurrentUserId);
            var saved = await profileService.GetProfileAsync(CurrentUserId);
            return Ok(saved);
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Import failed: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var profile = await profileService.GetProfileAsync(CurrentUserId);
        if (profile is null) return NoContent();
        return Ok(profile);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var profile = await profileService.GetProfileByIdAsync(id, CurrentUserId, IsAdmin);
        if (profile is null) return NotFound();
        return Ok(profile);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await profileService.GetStatusAsync(CurrentUserId);
        return Ok(status);
    }

    [HttpGet("all")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAll()
    {
        var summaries = await profileService.GetAllSummariesAsync();
        return Ok(summaries);
    }
}
```

- [ ] **Step 5: Build to verify**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio/LinkedInPortfolio.API
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
git add -A
git commit -m "feat: scope ProfileService/Controller to users, add Import endpoint"
```

---

## Task 4 (reference): LinkedInScraperService full implementation

> **This task was moved to Task 3 above.** The full `LinkedInScraperService.cs` implementation is below for reference — copy it into Task 3 Step 2.

- [ ] **Step 1: Replace `LinkedInPortfolio.API/Services/LinkedInScraperService.cs`**

```csharp
using System.Text.Json;
using PuppeteerSharp;

namespace LinkedInPortfolio.API.Services;

public class LinkedInScraperService(
    IHttpClientFactory httpClientFactory,
    ILogger<LinkedInScraperService> logger) : ILinkedInScraperService
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ProfileData> ScrapeProfileAsync(string profileUrl)
    {
        logger.LogInformation("Downloading Chromium if needed...");
        await new BrowserFetcher().DownloadAsync();

        logger.LogInformation("Launching headless browser...");
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            DefaultViewport = new ViewPortOptions { Width = 1280, Height = 900 },
            Args = [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-extensions",
                "--no-first-run"
            ]
        });

        await using var page = await browser.NewPageAsync();
        await page.SetUserAgentAsync(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/121.0.0.0 Safari/537.36");

        logger.LogInformation("Navigating to profile: {Url}", profileUrl);
        await page.GoToAsync(profileUrl, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
            Timeout = 60000
        });

        var landedUrl = page.Url;
        if (landedUrl.Contains("/login") || landedUrl.Contains("/checkpoint") || landedUrl.Contains("/authwall"))
            throw new UnauthorizedAccessException(
                "Your LinkedIn profile is set to private. " +
                "Go to LinkedIn → Settings → Visibility → Profile viewing options → set to Public, then try again.");

        logger.LogInformation("Profile page loaded at: {Url}", landedUrl);

        try { await page.WaitForSelectorAsync("h1", new WaitForSelectorOptions { Timeout = 15000 }); }
        catch { logger.LogWarning("h1 not found within 15s — continuing anyway"); }

        await Delay(2000, 3000);
        await ScrollPageFully(page);
        await ExpandSections(page);

        var mainHtml = await page.EvaluateExpressionAsync<string>("document.body.innerHTML");
        var dumpPath = Path.Combine(Path.GetTempPath(), "linkedin_main.html");
        await File.WriteAllTextAsync(dumpPath, mainHtml ?? string.Empty);
        logger.LogInformation("HTML saved to {Path} ({Size} bytes)", dumpPath, mainHtml?.Length ?? 0);

        var screenshotBytes = await page.ScreenshotDataAsync(new ScreenshotOptions { FullPage = true });
        await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "linkedin_profile.png"), screenshotBytes);

        logger.LogInformation("Extracting profile data...");
        return await ExtractAll(page);
    }

    private static async Task ScrollPageFully(IPage page)
    {
        for (int i = 0; i < 8; i++)
        {
            await page.EvaluateExpressionAsync("window.scrollBy(0, window.innerHeight * 1.5)");
            await Task.Delay(800);
        }
        await page.EvaluateExpressionAsync("window.scrollTo(0, 0)");
        await Task.Delay(1500);
    }

    private static async Task ExpandSections(IPage page)
    {
        await page.EvaluateExpressionAsync(@"
            (() => {
                const root = document.querySelector('#interop-outlet')?.shadowRoot ?? document;
                const buttons = root.querySelectorAll('button');
                for (const btn of buttons) {
                    const txt = (btn.textContent || btn.getAttribute('aria-label') || '').toLowerCase();
                    if (txt.includes('show more') || txt.includes('see more') || txt.includes('expand')) {
                        try { btn.click(); } catch {}
                    }
                }
            })()
        ");
        await Task.Delay(1000);
    }

    private async Task<ProfileData> ExtractAll(IPage page)
    {
        var json = await page.EvaluateExpressionAsync<string>(@"
            JSON.stringify((() => {
                const nameEl = document.querySelector('h1');
                const name = nameEl?.innerText?.trim() ?? '';

                let headline = '';
                if (nameEl) {
                    const tryEl = (el) => {
                        if (!el) return '';
                        const t = el.innerText?.trim() ?? '';
                        if (!t || t === name || t.length > 250 || el.tagName === 'BUTTON') return '';
                        if (el.querySelector('button, input, svg')) return '';
                        return t.split('\n')[0].trim();
                    };
                    headline = tryEl(nameEl.nextElementSibling);
                    if (!headline) headline = tryEl(nameEl.parentElement?.nextElementSibling);
                    if (!headline) headline = tryEl(nameEl.parentElement?.parentElement?.nextElementSibling);
                    if (!headline) {
                        const bw = Array.from(document.querySelectorAll('[class*=""break-words""]'))
                            .find(e => { const t = e.innerText?.trim(); return t && t !== name && t.length > 2 && t.length < 200; });
                        if (bw) headline = bw.innerText.trim().split('\n')[0];
                    }
                }

                const locationPatterns = ['Region', 'Area', 'City', 'Province', 'المنطقة', 'منطقة', 'مدينة', 'Saudi Arabia', 'الرياض'];
                const allLeafTexts = Array.from(document.querySelectorAll('span, button'))
                    .filter(e => e.children.length === 0)
                    .map(e => e.innerText?.trim())
                    .filter(t => t && t.length > 2 && t.length < 80);
                const location = allLeafTexts.find(t =>
                    locationPatterns.some(p => t.includes(p)) ||
                    (t.includes(',') && t.length < 60 && !/^\d/.test(t) && !/[.@#]/.test(t))
                ) ?? '';

                let about = '';
                const aboutKw = ['about', 'حول', 'نبذة', 'ملخص'];
                const aboutHeading = Array.from(document.querySelectorAll('h2, span, div'))
                    .find(e => aboutKw.includes((e.innerText?.trim() ?? '').toLowerCase()) && e.children.length === 0);
                if (aboutHeading) {
                    let el = aboutHeading;
                    for (let i = 0; i < 6; i++) {
                        el = el.parentElement;
                        if (!el) break;
                        const longText = Array.from(el.querySelectorAll('p, div, span'))
                            .map(e => e.innerText?.trim())
                            .find(t => t && t.length > 50 && t !== name && t !== headline);
                        if (longText) { about = longText; break; }
                    }
                }

                const photoSrc = (name ? document.querySelector('img[alt=""' + name + '""]')?.src : null)
                    ?? document.querySelector('img.pv-top-card-profile-picture__image--show')?.src
                    ?? document.querySelector('img[class*=""profile-picture""]')?.src
                    ?? '';

                const findSectionItems = (keywords) => {
                    for (const kw of keywords) {
                        const byId = document.getElementById(kw);
                        if (byId) {
                            const items = byId.querySelectorAll('li');
                            if (items.length > 0) return extractListItems(items);
                        }
                    }
                    const headings = Array.from(document.querySelectorAll('h2, h3, span, div'))
                        .filter(e => {
                            const t = (e.innerText ?? '').trim().toLowerCase();
                            return keywords.map(k => k.toLowerCase()).includes(t) && e.children.length === 0;
                        });
                    for (const h of headings) {
                        let el = h;
                        for (let i = 0; i < 10; i++) {
                            el = el.parentElement;
                            if (!el) break;
                            const items = el.querySelectorAll('li');
                            if (items.length > 0) return extractListItems(items);
                        }
                    }
                    return [];
                };

                const extractListItems = (items) =>
                    Array.from(items).map(item => {
                        const ariaHidden = Array.from(item.querySelectorAll('span[aria-hidden=""true""]'))
                            .map(e => e.innerText?.trim()).filter(t => t && t.length > 0);
                        const allTexts = ariaHidden.length > 0 ? ariaHidden
                            : [...new Set(Array.from(item.querySelectorAll('span, p, h3, h4, a'))
                                .map(e => e.innerText?.trim()).filter(t => t && t.length > 0))];
                        return { texts: [...new Set(allTexts)], link: item.querySelector('a[href]')?.href ?? '' };
                    }).filter(i => i.texts.length > 0);

                const expItems = findSectionItems(['experience', 'خبرة', 'خبرات', 'الخبرة']);
                const experiences = expItems.map(({ texts }) => {
                    const dateRange = texts.find(t => /[–\-]/.test(t) && /\d{4}|present|الحاضر/i.test(t)) ?? '';
                    const parts = dateRange.split(/\s*[–\-]\s*/);
                    return {
                        Title: texts[0] ?? '', Company: texts[1] ?? '',
                        StartDate: parts[0] ?? '', EndDate: parts[1] ?? '',
                        Description: texts.find(t => t.length > 50) ?? '',
                        IsCurrent: /present|الحاضر/i.test(dateRange)
                    };
                }).filter(e => e.Title.length > 0);

                const eduItems = findSectionItems(['education', 'تعليم', 'التعليم']);
                const educations = eduItems.map(({ texts }) => {
                    const years = (texts.find(t => /\d{4}/.test(t)) ?? '').match(/\d{4}/g) ?? [];
                    return { School: texts[0] ?? '', Degree: texts[1] ?? '', FieldOfStudy: texts[2] ?? '', StartYear: years[0] ?? '', EndYear: years[1] ?? '' };
                }).filter(e => e.School.length > 0);

                const skillItems = findSectionItems(['skills', 'مهارات', 'المهارات']);
                const skills = skillItems.map(({ texts }) => ({
                    Name: texts[0] ?? '', EndorsementCount: parseInt(texts.find(t => /^\d+$/.test(t)) ?? '0')
                })).filter(s => s.Name.length > 0);

                const projItems = findSectionItems(['projects', 'مشاريع', 'المشاريع']);
                const projects = projItems.map(({ texts, link }) => ({
                    Title: texts[0] ?? '', Description: texts[1] ?? '', Url: link, StartDate: '', EndDate: ''
                })).filter(p => p.Title.length > 0);

                const certItems = findSectionItems(['licenses & certifications', 'certifications', 'شهادات', 'الشهادات', 'تراخيص']);
                const certifications = certItems.map(({ texts, link }) => ({
                    Name: texts[0] ?? '', IssuingOrganization: texts[1] ?? '', IssueDate: texts[2] ?? '', CredentialUrl: link
                })).filter(c => c.Name.length > 0);

                return { name, headline, location, about, photoSrc, experiences, educations, skills, projects, certifications };
            })())
        ");

        if (string.IsNullOrEmpty(json))
            return new ProfileData { FetchedAt = DateTime.UtcNow };

        var d = JsonSerializer.Deserialize<JsonElement>(json);
        var photoBase64 = await DownloadPhoto(d.GetProperty("photoSrc").GetString() ?? string.Empty);

        return new ProfileData
        {
            FetchedAt = DateTime.UtcNow,
            Name = d.GetProperty("name").GetString() ?? string.Empty,
            Headline = d.GetProperty("headline").GetString() ?? string.Empty,
            Location = d.GetProperty("location").GetString() ?? string.Empty,
            About = d.GetProperty("about").GetString() ?? string.Empty,
            PhotoBase64 = photoBase64,
            Experiences = JsonSerializer.Deserialize<List<ExperienceData>>(d.GetProperty("experiences").GetRawText(), _jsonOpts) ?? [],
            Educations = JsonSerializer.Deserialize<List<EducationData>>(d.GetProperty("educations").GetRawText(), _jsonOpts) ?? [],
            Skills = JsonSerializer.Deserialize<List<SkillData>>(d.GetProperty("skills").GetRawText(), _jsonOpts) ?? [],
            Projects = JsonSerializer.Deserialize<List<ProjectData>>(d.GetProperty("projects").GetRawText(), _jsonOpts) ?? [],
            Certifications = JsonSerializer.Deserialize<List<CertificationData>>(d.GetProperty("certifications").GetRawText(), _jsonOpts) ?? []
        };
    }

    private async Task<string> DownloadPhoto(string src)
    {
        try
        {
            if (string.IsNullOrEmpty(src) || src.StartsWith("data:")) return src ?? string.Empty;
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("Referer", "https://www.linkedin.com/");
            var bytes = await client.GetByteArrayAsync(src);
            return Convert.ToBase64String(bytes);
        }
        catch { return string.Empty; }
    }

    private static Task Delay(int minMs = 800, int maxMs = 1800)
        => Task.Delay(Random.Shared.Next(minMs, maxMs));
}
```

- [ ] **Step 3: Build to verify**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio/LinkedInPortfolio.API
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
git add -A
git commit -m "feat: rewrite scraper for headless public URL (no login/cookies)"
```

---

## Task 5: Dockerfile — Chromium system dependencies

**Files:**
- Modify: `LinkedInPortfolio.API/Dockerfile`

- [ ] **Step 1: Replace `LinkedInPortfolio.API/Dockerfile`**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LinkedInPortfolio.API/LinkedInPortfolio.API.csproj LinkedInPortfolio.API/
RUN dotnet restore LinkedInPortfolio.API/LinkedInPortfolio.API.csproj

COPY LinkedInPortfolio.API/ LinkedInPortfolio.API/
WORKDIR /src/LinkedInPortfolio.API
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    ca-certificates \
    libnss3 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libasound2 \
    libpangocairo-1.0-0 \
    libpango-1.0-0 \
    libcairo2 \
    libgdk-pixbuf-2.0-0 \
    libgtk-3-0 \
    libx11-xcb1 \
    libxcb-dri3-0 \
    fonts-liberation \
    xdg-utils \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LinkedInPortfolio.API.dll"]
```

- [ ] **Step 2: Commit**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
git add LinkedInPortfolio.API/Dockerfile
git commit -m "feat: add Chromium system dependencies to API Dockerfile"
```

---

## Task 6: Tests — AuthController + ProfileController import

**Files:**
- Create: `LinkedInPortfolio.Tests/AuthControllerTests.cs`
- Create: `LinkedInPortfolio.Tests/ProfileImportTests.cs`

- [ ] **Step 1: Create `LinkedInPortfolio.Tests/AuthControllerTests.cs`**

```csharp
using LinkedInPortfolio.API.Controllers;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LinkedInPortfolio.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_WithValidCredentials_Returns200WithToken()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(s => s.RegisterAsync("user@test.com", "password123"))
            .ReturnsAsync(new AuthResponseDto { Token = "jwt.token.here" });

        var controller = new AuthController(mockAuth.Object);
        var result = await controller.Register(new RegisterRequest("user@test.com", "password123"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<AuthResponseDto>(ok.Value);
        Assert.Equal("jwt.token.here", dto.Token);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(s => s.RegisterAsync("dup@test.com", "password123"))
            .ReturnsAsync((AuthResponseDto?)null);

        var controller = new AuthController(mockAuth.Object);
        var result = await controller.Register(new RegisterRequest("dup@test.com", "password123"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400WithoutCallingService()
    {
        var mockAuth = new Mock<IAuthService>();

        var controller = new AuthController(mockAuth.Object);
        var result = await controller.Register(new RegisterRequest("user@test.com", "short"));

        Assert.IsType<BadRequestObjectResult>(result);
        mockAuth.Verify(s => s.RegisterAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(s => s.LoginAsync("user@test.com", "password123"))
            .ReturnsAsync(new AuthResponseDto { Token = "jwt.token.here" });

        var controller = new AuthController(mockAuth.Object);
        var result = await controller.Login(new LoginRequest("user@test.com", "password123"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<AuthResponseDto>(ok.Value);
        Assert.Equal("jwt.token.here", dto.Token);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var mockAuth = new Mock<IAuthService>();
        mockAuth.Setup(s => s.LoginAsync("user@test.com", "wrongpass"))
            .ReturnsAsync((AuthResponseDto?)null);

        var controller = new AuthController(mockAuth.Object);
        var result = await controller.Login(new LoginRequest("user@test.com", "wrongpass"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
```

- [ ] **Step 2: Create `LinkedInPortfolio.Tests/ProfileImportTests.cs`**

```csharp
using System.Security.Claims;
using LinkedInPortfolio.API.Controllers;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LinkedInPortfolio.Tests;

public class ProfileImportTests
{
    private static ProfileController BuildController(
        ILinkedInScraperService scraper,
        IProfileService profileService,
        int userId = 1,
        bool isAdmin = false)
    {
        var controller = new ProfileController(profileService, scraper);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("isAdmin", isAdmin.ToString().ToLower())
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task Import_WithValidUrl_Returns200WithProfile()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();
        var profileData = new ProfileData { Name = "Ahmed", FetchedAt = DateTime.UtcNow };

        mockScraper.Setup(s => s.ScrapeProfileAsync("https://www.linkedin.com/in/ahmedomran"))
            .ReturnsAsync(profileData);
        mockProfileService.Setup(s => s.SaveProfileAsync(It.IsAny<ProfileData>(), 1))
            .Returns(Task.CompletedTask);
        mockProfileService.Setup(s => s.GetProfileAsync(1))
            .ReturnsAsync(new ProfileDto { Name = "Ahmed" });

        var controller = BuildController(mockScraper.Object, mockProfileService.Object);
        var result = await controller.Import(new ImportRequest("https://www.linkedin.com/in/ahmedomran"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ProfileDto>(ok.Value);
        Assert.Equal("Ahmed", dto.Name);
    }

    [Fact]
    public async Task Import_WithInvalidUrl_Returns400WithoutCallingScraper()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();

        var controller = BuildController(mockScraper.Object, mockProfileService.Object);
        var result = await controller.Import(new ImportRequest("https://www.example.com/not-linkedin"));

        Assert.IsType<BadRequestObjectResult>(result);
        mockScraper.Verify(s => s.ScrapeProfileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Import_WhenProfileIsPrivate_Returns400()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();

        mockScraper.Setup(s => s.ScrapeProfileAsync(It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException("Profile is private."));

        var controller = BuildController(mockScraper.Object, mockProfileService.Object);
        var result = await controller.Import(new ImportRequest("https://www.linkedin.com/in/private-user"));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Import_WhenScraperThrowsGenericException_Returns500()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();

        mockScraper.Setup(s => s.ScrapeProfileAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Chromium crashed"));

        var controller = BuildController(mockScraper.Object, mockProfileService.Object);
        var result = await controller.Import(new ImportRequest("https://www.linkedin.com/in/ahmedomran"));

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
```

- [ ] **Step 3: Run tests**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
dotnet test LinkedInPortfolio.Tests/
```

Expected: All 9 tests pass.

- [ ] **Step 4: Commit**

```bash
git add LinkedInPortfolio.Tests/AuthControllerTests.cs \
        LinkedInPortfolio.Tests/ProfileImportTests.cs
git commit -m "test: add AuthController and ProfileImport tests"
```

---

## Task 7: Frontend — types + authApi + profileApi update

**Files:**
- Modify: `linkedin-portfolio-ui/src/types/profile.ts`
- Create: `linkedin-portfolio-ui/src/api/authApi.ts`
- Modify: `linkedin-portfolio-ui/src/api/profileApi.ts`

- [ ] **Step 1: Add auth types to `linkedin-portfolio-ui/src/types/profile.ts`**

Append at the end of the file:

```typescript
export interface AuthResponseDto {
  token: string
}

export interface ImportResultDto {
  name: string
  headline: string
}
```

- [ ] **Step 2: Create `linkedin-portfolio-ui/src/api/authApi.ts`**

```typescript
import axios from 'axios'
import type { AuthResponseDto } from '../types/profile'

const api = axios.create({ baseURL: '/api' })

export async function register(email: string, password: string): Promise<AuthResponseDto> {
  const res = await api.post<AuthResponseDto>('/auth/register', { email, password })
  return res.data
}

export async function login(email: string, password: string): Promise<AuthResponseDto> {
  const res = await api.post<AuthResponseDto>('/auth/login', { email, password })
  return res.data
}
```

- [ ] **Step 3: Replace `linkedin-portfolio-ui/src/api/profileApi.ts`**

```typescript
import axios from 'axios'
import type { ProfileDto, ProfileStatusDto, ProfileSummaryDto } from '../types/profile'

const api = axios.create({ baseURL: '/api' })

api.interceptors.request.use(config => {
  const token = localStorage.getItem('token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

export async function fetchProfile(): Promise<ProfileDto | null> {
  const res = await api.get<ProfileDto>('/profile')
  if (res.status === 204) return null
  return res.data
}

export async function fetchAllProfiles(): Promise<ProfileSummaryDto[]> {
  const res = await api.get<ProfileSummaryDto[]>('/profile/all')
  return res.data
}

export async function fetchProfileById(id: number): Promise<ProfileDto> {
  const res = await api.get<ProfileDto>(`/profile/${id}`)
  return res.data
}

export async function fetchStatus(): Promise<ProfileStatusDto> {
  const res = await api.get<ProfileStatusDto>('/profile/status')
  return res.data
}

export async function importProfile(linkedInUrl: string): Promise<ProfileDto> {
  const res = await api.post<ProfileDto>('/profile/import', { linkedInUrl })
  return res.data
}
```

- [ ] **Step 4: Commit**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
git add linkedin-portfolio-ui/src/types/profile.ts \
        linkedin-portfolio-ui/src/api/authApi.ts \
        linkedin-portfolio-ui/src/api/profileApi.ts
git commit -m "feat: add auth types, authApi, JWT interceptor in profileApi"
```

---

## Task 8: Frontend — useAuth hook + auth pages + ProtectedRoute

**Files:**
- Create: `linkedin-portfolio-ui/src/hooks/useAuth.ts`
- Create: `linkedin-portfolio-ui/src/components/ProtectedRoute.tsx`
- Create: `linkedin-portfolio-ui/src/pages/LoginPage.tsx`
- Create: `linkedin-portfolio-ui/src/pages/RegisterPage.tsx`

- [ ] **Step 1: Create `linkedin-portfolio-ui/src/hooks/useAuth.ts`**

```typescript
export interface AuthUser {
  userId: number
  email: string
  isAdmin: boolean
}

function decodeToken(token: string): AuthUser | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    return {
      userId: parseInt(payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']),
      email: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'],
      isAdmin: payload['isAdmin'] === 'true'
    }
  } catch {
    return null
  }
}

export function useAuth() {
  const token = localStorage.getItem('token')
  const user = token ? decodeToken(token) : null

  function logout() {
    localStorage.removeItem('token')
    window.location.href = '/login'
  }

  return { user, isAdmin: user?.isAdmin ?? false, logout, isAuthenticated: !!user }
}
```

- [ ] **Step 2: Create `linkedin-portfolio-ui/src/components/ProtectedRoute.tsx`**

```tsx
import { Navigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

interface Props {
  children: React.ReactNode
  adminOnly?: boolean
}

export function ProtectedRoute({ children, adminOnly = false }: Props) {
  const { isAuthenticated, isAdmin } = useAuth()

  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (adminOnly && !isAdmin) return <Navigate to="/" replace />

  return <>{children}</>
}
```

- [ ] **Step 3: Create `linkedin-portfolio-ui/src/pages/LoginPage.tsx`**

```tsx
import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { login } from '../api/authApi'

export function LoginPage() {
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const res = await login(email, password)
      localStorage.setItem('token', res.token)
      navigate('/')
    } catch (err: any) {
      setError(err.response?.data?.message ?? 'Login failed. Check your credentials.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center px-4">
      <div className="w-full max-w-sm bg-white rounded-2xl shadow p-8 space-y-6">
        <h1 className="text-2xl font-bold text-gray-900">Sign in</h1>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
            <input
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              required
              className="w-full border border-gray-300 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Password</label>
            <input
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              required
              className="w-full border border-gray-300 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
            />
          </div>

          {error && <p className="text-sm text-red-600">{error}</p>}

          <button
            type="submit"
            disabled={loading}
            className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold rounded-xl transition-colors"
          >
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <p className="text-sm text-center text-gray-500">
          No account?{' '}
          <Link to="/register" className="text-blue-600 hover:underline">Register</Link>
        </p>
      </div>
    </main>
  )
}
```

- [ ] **Step 4: Create `linkedin-portfolio-ui/src/pages/RegisterPage.tsx`**

```tsx
import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { register } from '../api/authApi'

export function RegisterPage() {
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const res = await register(email, password)
      localStorage.setItem('token', res.token)
      navigate('/import')
    } catch (err: any) {
      setError(err.response?.data?.message ?? 'Registration failed. Try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center px-4">
      <div className="w-full max-w-sm bg-white rounded-2xl shadow p-8 space-y-6">
        <h1 className="text-2xl font-bold text-gray-900">Create account</h1>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
            <input
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              required
              className="w-full border border-gray-300 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Password <span className="text-gray-400">(min 8 characters)</span></label>
            <input
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              required
              minLength={8}
              className="w-full border border-gray-300 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
            />
          </div>

          {error && <p className="text-sm text-red-600">{error}</p>}

          <button
            type="submit"
            disabled={loading}
            className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold rounded-xl transition-colors"
          >
            {loading ? 'Creating account…' : 'Create account'}
          </button>
        </form>

        <p className="text-sm text-center text-gray-500">
          Already have an account?{' '}
          <Link to="/login" className="text-blue-600 hover:underline">Sign in</Link>
        </p>
      </div>
    </main>
  )
}
```

- [ ] **Step 5: Commit**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
git add linkedin-portfolio-ui/src/hooks/ \
        linkedin-portfolio-ui/src/components/ProtectedRoute.tsx \
        linkedin-portfolio-ui/src/pages/LoginPage.tsx \
        linkedin-portfolio-ui/src/pages/RegisterPage.tsx
git commit -m "feat: add useAuth hook, ProtectedRoute, Login and Register pages"
```

---

## Task 9: Frontend — ImportPage + App.tsx routing + update existing pages

**Files:**
- Create: `linkedin-portfolio-ui/src/pages/ImportPage.tsx`
- Modify: `linkedin-portfolio-ui/src/App.tsx`
- Modify: `linkedin-portfolio-ui/src/pages/PortfolioPage.tsx`
- Modify: `linkedin-portfolio-ui/src/pages/AdminPage.tsx`

- [ ] **Step 1: Create `linkedin-portfolio-ui/src/pages/ImportPage.tsx`**

```tsx
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { importProfile } from '../api/profileApi'
import { useAuth } from '../hooks/useAuth'

export function ImportPage() {
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const [url, setUrl] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleImport(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await importProfile(url)
      navigate('/')
    } catch (err: any) {
      setError(err.response?.data?.message ?? 'Import failed. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center px-4">
      <div className="w-full max-w-lg bg-white rounded-2xl shadow p-8 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900">Import LinkedIn Profile</h1>
          <button onClick={logout} className="text-sm text-gray-400 hover:text-gray-600">Sign out</button>
        </div>

        <p className="text-sm text-gray-500">
          Signed in as <span className="font-medium text-gray-700">{user?.email}</span>
        </p>

        <div className="bg-blue-50 rounded-xl p-4 text-sm text-blue-700 space-y-1">
          <p className="font-medium text-blue-800">Before importing:</p>
          <p>Make sure your LinkedIn profile is set to <strong>Public</strong>.</p>
          <p>LinkedIn → Settings → Visibility → Profile viewing options → Public</p>
        </div>

        <form onSubmit={handleImport} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Your LinkedIn Profile URL</label>
            <input
              type="url"
              value={url}
              onChange={e => setUrl(e.target.value)}
              placeholder="https://www.linkedin.com/in/your-username"
              required
              className="w-full border border-gray-300 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
            />
          </div>

          {error && (
            <div className="p-3 bg-red-50 rounded-xl text-sm text-red-700">{error}</div>
          )}

          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold rounded-xl transition-colors"
          >
            {loading ? 'Importing… (this takes ~30 seconds)' : 'Import from LinkedIn'}
          </button>
        </form>
      </div>
    </main>
  )
}
```

- [ ] **Step 2: Replace `linkedin-portfolio-ui/src/App.tsx`**

```tsx
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { PortfolioPage } from './pages/PortfolioPage'
import { AdminPage } from './pages/AdminPage'
import { ProfileDetailPage } from './pages/ProfileDetailPage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { ImportPage } from './pages/ImportPage'
import { ProtectedRoute } from './components/ProtectedRoute'

const queryClient = new QueryClient()

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <div className="min-h-screen bg-gray-50">
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/" element={<ProtectedRoute><PortfolioPage /></ProtectedRoute>} />
            <Route path="/import" element={<ProtectedRoute><ImportPage /></ProtectedRoute>} />
            <Route path="/admin" element={<ProtectedRoute adminOnly><AdminPage /></ProtectedRoute>} />
            <Route path="/profile/:id" element={<ProtectedRoute><ProfileDetailPage /></ProtectedRoute>} />
          </Routes>
        </div>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
```

- [ ] **Step 3: Update `linkedin-portfolio-ui/src/pages/PortfolioPage.tsx`** — change empty state link + add nav

Replace the full file content:

```tsx
import { useQuery } from '@tanstack/react-query'
import { fetchProfile } from '../api/profileApi'
import { ProfileHeader } from '../components/ProfileHeader'
import { AboutSection } from '../components/AboutSection'
import { ExperienceSection } from '../components/ExperienceSection'
import { EducationSection } from '../components/EducationSection'
import { SkillsSection } from '../components/SkillsSection'
import { ProjectsSection } from '../components/ProjectsSection'
import { CertificationsSection } from '../components/CertificationsSection'
import { LoadingSpinner } from '../components/LoadingSpinner'
import { Link } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

export function PortfolioPage() {
  const { isAdmin, logout } = useAuth()
  const { data: profile, isLoading, isError } = useQuery({
    queryKey: ['profile'],
    queryFn: fetchProfile,
    retry: false,
  })

  if (isLoading) return <LoadingSpinner message="Loading profile..." />

  if (isError) return (
    <div className="text-center py-20 text-red-500">
      Failed to load profile. Is the API running?
    </div>
  )

  return (
    <main className="max-w-3xl mx-auto px-4 py-8 space-y-6">
      <div className="flex justify-end gap-4 text-sm">
        <Link to="/import" className="text-blue-600 hover:underline">Import / Update</Link>
        {isAdmin && <Link to="/admin" className="text-blue-600 hover:underline">Admin</Link>}
        <button onClick={logout} className="text-gray-400 hover:text-gray-600">Sign out</button>
      </div>

      {!profile ? (
        <div className="text-center py-20 text-gray-500">
          <p className="text-lg">No profile data yet.</p>
          <Link to="/import" className="mt-3 inline-block text-blue-600 hover:underline">
            Import from LinkedIn →
          </Link>
        </div>
      ) : (
        <>
          <ProfileHeader
            name={profile.name}
            headline={profile.headline}
            location={profile.location}
            photoBase64={profile.photoBase64}
          />
          <AboutSection about={profile.about} />
          <ExperienceSection experience={profile.experience} />
          <EducationSection education={profile.education} />
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <SkillsSection skills={profile.skills} />
            <CertificationsSection certifications={profile.certifications} />
          </div>
          <ProjectsSection projects={profile.projects} />
          <p className="text-center text-xs text-gray-400">
            Last imported: {new Date(profile.fetchedAt).toLocaleString()}
          </p>
        </>
      )}
    </main>
  )
}
```

- [ ] **Step 4: Replace `linkedin-portfolio-ui/src/pages/AdminPage.tsx`** — remove sync card, show all users' profiles

```tsx
import { useQuery } from '@tanstack/react-query'
import { fetchAllProfiles } from '../api/profileApi'
import { LoadingSpinner } from '../components/LoadingSpinner'
import { Link } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

export function AdminPage() {
  const { logout } = useAuth()

  const { data: profiles, isLoading } = useQuery({
    queryKey: ['all-profiles'],
    queryFn: fetchAllProfiles,
  })

  return (
    <main className="max-w-2xl mx-auto px-4 py-12">
      <div className="bg-white rounded-2xl shadow p-8 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900">Admin — All Profiles</h1>
          <div className="flex gap-4 text-sm">
            <Link to="/" className="text-blue-600 hover:underline">← Portfolio</Link>
            <button onClick={logout} className="text-gray-400 hover:text-gray-600">Sign out</button>
          </div>
        </div>

        {isLoading ? (
          <LoadingSpinner message="Loading profiles..." />
        ) : !profiles || profiles.length === 0 ? (
          <p className="text-sm text-gray-400">No profiles imported yet.</p>
        ) : (
          <div className="divide-y divide-gray-100">
            {profiles.map(p => (
              <div key={p.id} className="py-3 flex items-center justify-between gap-4">
                <div className="min-w-0">
                  <p className="font-medium text-gray-900 truncate">{p.name || '(no name)'}</p>
                  <p className="text-xs text-blue-600 truncate">{p.userEmail}</p>
                  <p className="text-xs text-gray-500 truncate">{p.headline || '—'}</p>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {new Date(p.fetchedAt).toLocaleString()} &nbsp;·&nbsp;
                    {p.experienceCount} exp &nbsp;·&nbsp; {p.skillCount} skills
                  </p>
                </div>
                <Link
                  to={`/profile/${p.id}`}
                  className="flex-shrink-0 px-4 py-1.5 text-sm bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
                >
                  View
                </Link>
              </div>
            ))}
          </div>
        )}
      </div>
    </main>
  )
}
```

- [ ] **Step 5: Commit**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
git add linkedin-portfolio-ui/src/pages/ImportPage.tsx \
        linkedin-portfolio-ui/src/App.tsx \
        linkedin-portfolio-ui/src/pages/PortfolioPage.tsx \
        linkedin-portfolio-ui/src/pages/AdminPage.tsx
git commit -m "feat: add ImportPage, update routing, update portfolio and admin pages"
```

---

## Task 10: Docker rebuild + end-to-end verification

- [ ] **Step 1: Rebuild and restart**

```bash
cd /Users/ahmedomran/Tuwaiq/Projects/LinkedInPortfolio
docker-compose down
docker-compose build
docker-compose up -d
```

- [ ] **Step 2: Wait for healthy status**

```bash
docker-compose ps
```

Expected: `api` shows `healthy`, `frontend` shows `running`.

- [ ] **Step 3: Verify register endpoint**

```bash
curl -s -X POST http://localhost:3000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"password123"}' | python3 -m json.tool
```

Expected: `{"token":"eyJ..."}` — a JWT token.

- [ ] **Step 4: Verify login endpoint**

```bash
curl -s -X POST http://localhost:3000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"password123"}' | python3 -m json.tool
```

Expected: Same JWT token format.

- [ ] **Step 5: Verify protected endpoint requires token**

```bash
curl -s http://localhost:3000/api/profile/status
```

Expected: `401 Unauthorized`

- [ ] **Step 6: Verify frontend register flow**

Open `http://localhost:3000/register` — fill in email + password → submit → redirects to `/import`. The import page shows the LinkedIn URL input.

- [ ] **Step 7: Verify the full import flow**

On the import page, paste a public LinkedIn URL and click Import. Expected: ~30 second wait, then redirect to `/` showing the imported profile.

---

## Verification Checklist

- [ ] `dotnet build` passes with 0 errors after all backend changes
- [ ] `docker-compose build` succeeds — all Chromium apt packages install
- [ ] `POST /api/auth/register` returns JWT; first user gets `isAdmin=true`
- [ ] `POST /api/auth/login` returns JWT
- [ ] `GET /api/profile/status` without token returns 401
- [ ] `GET /api/profile/status` with valid token returns 200
- [ ] `GET /api/profile/all` with non-admin token returns 403
- [ ] `http://localhost:3000` redirects to `/login` when not authenticated
- [ ] Register → redirects to `/import`
- [ ] Import with valid public LinkedIn URL → redirects to `/` with profile data
- [ ] Import with private URL → shows error message about making profile public
- [ ] `/admin` only accessible to first registered user
