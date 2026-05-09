# LinkedIn Portfolio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a full-stack app that scrapes a LinkedIn profile with PuppeteerSharp, stores the data in SQL Server LocalDB, and displays it as a portfolio website.

**Architecture:** .NET Core 8 Web API handles scraping (PuppeteerSharp), persistence (EF Core + SQL Server LocalDB), and serving. React (Vite + TypeScript + TailwindCSS + TanStack Query) is a pure display layer. A manual sync button in `/admin` triggers the scraper; `/` shows the portfolio.

**Tech Stack:** .NET 8, EF Core 8, PuppeteerSharp 20+, SQL Server LocalDB, xUnit, Moq, React 18, Vite, TypeScript, TailwindCSS 3, TanStack Query v5, React Router v6

---

## File Structure

```
D:\Projects\LinkedInPortfolio\
├── LinkedInPortfolio.sln
├── LinkedInPortfolio.API/
│   ├── LinkedInPortfolio.API.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Data/
│   │   └── AppDbContext.cs
│   ├── Models/
│   │   ├── ProfileSnapshot.cs
│   │   ├── Experience.cs
│   │   ├── Education.cs
│   │   ├── Skill.cs
│   │   ├── Project.cs
│   │   └── Certification.cs
│   ├── DTOs/
│   │   ├── ProfileDto.cs          ← API response shape
│   │   ├── ProfileStatusDto.cs    ← /api/profile/status response
│   │   └── SyncResultDto.cs       ← /api/sync response
│   ├── Services/
│   │   ├── ILinkedInScraperService.cs
│   │   ├── LinkedInScraperService.cs
│   │   ├── IProfileService.cs
│   │   └── ProfileService.cs
│   └── Controllers/
│       ├── ProfileController.cs
│       └── SyncController.cs
├── LinkedInPortfolio.Tests/
│   ├── LinkedInPortfolio.Tests.csproj
│   ├── ProfileServiceTests.cs
│   └── SyncControllerTests.cs
└── linkedin-portfolio-ui/
    ├── package.json
    ├── vite.config.ts
    ├── tailwind.config.js
    ├── postcss.config.js
    ├── index.html
    └── src/
        ├── main.tsx
        ├── App.tsx
        ├── types/
        │   └── profile.ts
        ├── api/
        │   └── profileApi.ts
        ├── pages/
        │   ├── PortfolioPage.tsx
        │   └── AdminPage.tsx
        └── components/
            ├── ProfileHeader.tsx
            ├── AboutSection.tsx
            ├── ExperienceSection.tsx
            ├── EducationSection.tsx
            ├── SkillsSection.tsx
            ├── ProjectsSection.tsx
            ├── CertificationsSection.tsx
            └── LoadingSpinner.tsx
```

---

## Task 1: Scaffold .NET Solution and API Project

**Files:**
- Create: `LinkedInPortfolio.sln`
- Create: `LinkedInPortfolio.API/LinkedInPortfolio.API.csproj`
- Create: `LinkedInPortfolio.API/appsettings.json`
- Create: `LinkedInPortfolio.API/appsettings.Development.json`

- [ ] **Step 1: Create solution and API project**

```powershell
cd D:\Projects\LinkedInPortfolio
dotnet new sln -n LinkedInPortfolio
dotnet new webapi -n LinkedInPortfolio.API --use-controllers
dotnet sln add LinkedInPortfolio.API/LinkedInPortfolio.API.csproj
```

Expected: Solution created, API project scaffolded with Controllers support.

- [ ] **Step 2: Add NuGet packages**

```powershell
cd LinkedInPortfolio.API
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package PuppeteerSharp --version 20.0.2
```

Expected: All packages restore without errors.

- [ ] **Step 3: Replace appsettings.json**

Replace the contents of `LinkedInPortfolio.API/appsettings.json` with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LinkedInPortfolio;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "LinkedIn": {
    "Email": "your@email.com",
    "Password": "yourpassword",
    "ProfileSlug": "your-profile-slug"
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

- [ ] **Step 4: Delete the generated WeatherForecast files**

```powershell
Remove-Item LinkedInPortfolio.API/Controllers/WeatherForecastController.cs
Remove-Item LinkedInPortfolio.API/WeatherForecast.cs
```

- [ ] **Step 5: Verify project builds**

```powershell
cd D:\Projects\LinkedInPortfolio
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6: Commit**

```powershell
cd D:\Projects\LinkedInPortfolio
git add LinkedInPortfolio.sln LinkedInPortfolio.API/
git commit -m "feat: scaffold .NET solution and API project"
```

---

## Task 2: Create Entity Models

**Files:**
- Create: `LinkedInPortfolio.API/Models/ProfileSnapshot.cs`
- Create: `LinkedInPortfolio.API/Models/Experience.cs`
- Create: `LinkedInPortfolio.API/Models/Education.cs`
- Create: `LinkedInPortfolio.API/Models/Skill.cs`
- Create: `LinkedInPortfolio.API/Models/Project.cs`
- Create: `LinkedInPortfolio.API/Models/Certification.cs`

- [ ] **Step 1: Create ProfileSnapshot.cs**

```csharp
// LinkedInPortfolio.API/Models/ProfileSnapshot.cs
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
```

- [ ] **Step 2: Create Experience.cs**

```csharp
// LinkedInPortfolio.API/Models/Experience.cs
namespace LinkedInPortfolio.API.Models;

public class Experience
{
    public int Id { get; set; }
    public int ProfileSnapshotId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}
```

- [ ] **Step 3: Create Education.cs**

```csharp
// LinkedInPortfolio.API/Models/Education.cs
namespace LinkedInPortfolio.API.Models;

public class Education
{
    public int Id { get; set; }
    public int ProfileSnapshotId { get; set; }
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    public string StartYear { get; set; } = string.Empty;
    public string EndYear { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Create Skill.cs**

```csharp
// LinkedInPortfolio.API/Models/Skill.cs
namespace LinkedInPortfolio.API.Models;

public class Skill
{
    public int Id { get; set; }
    public int ProfileSnapshotId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EndorsementCount { get; set; }
}
```

- [ ] **Step 5: Create Project.cs**

```csharp
// LinkedInPortfolio.API/Models/Project.cs
namespace LinkedInPortfolio.API.Models;

public class Project
{
    public int Id { get; set; }
    public int ProfileSnapshotId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}
```

- [ ] **Step 6: Create Certification.cs**

```csharp
// LinkedInPortfolio.API/Models/Certification.cs
namespace LinkedInPortfolio.API.Models;

public class Certification
{
    public int Id { get; set; }
    public int ProfileSnapshotId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string CredentialUrl { get; set; } = string.Empty;
}
```

- [ ] **Step 7: Verify build**

```powershell
cd D:\Projects\LinkedInPortfolio && dotnet build
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 8: Commit**

```powershell
git add LinkedInPortfolio.API/Models/
git commit -m "feat: add EF Core entity models"
```

---

## Task 3: Create AppDbContext

**Files:**
- Create: `LinkedInPortfolio.API/Data/AppDbContext.cs`

- [ ] **Step 1: Create AppDbContext.cs**

```csharp
// LinkedInPortfolio.API/Data/AppDbContext.cs
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
```

- [ ] **Step 2: Verify build**

```powershell
cd D:\Projects\LinkedInPortfolio && dotnet build
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3: Commit**

```powershell
git add LinkedInPortfolio.API/Data/
git commit -m "feat: add AppDbContext with EF Core configuration"
```

---

## Task 4: Create DTOs and Internal Data Transfer Type

**Files:**
- Create: `LinkedInPortfolio.API/DTOs/ProfileDto.cs`
- Create: `LinkedInPortfolio.API/DTOs/ProfileStatusDto.cs`
- Create: `LinkedInPortfolio.API/DTOs/SyncResultDto.cs`
- Create: `LinkedInPortfolio.API/Services/ProfileData.cs`

- [ ] **Step 1: Create ProfileDto.cs**

```csharp
// LinkedInPortfolio.API/DTOs/ProfileDto.cs
namespace LinkedInPortfolio.API.DTOs;

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
```

- [ ] **Step 2: Create ProfileStatusDto.cs**

```csharp
// LinkedInPortfolio.API/DTOs/ProfileStatusDto.cs
namespace LinkedInPortfolio.API.DTOs;

public class ProfileStatusDto
{
    public DateTime? LastSyncedAt { get; set; }
    public int ExperienceCount { get; set; }
    public int EducationCount { get; set; }
    public int SkillCount { get; set; }
    public int ProjectCount { get; set; }
    public int CertificationCount { get; set; }
}
```

- [ ] **Step 3: Create SyncResultDto.cs**

```csharp
// LinkedInPortfolio.API/DTOs/SyncResultDto.cs
namespace LinkedInPortfolio.API.DTOs;

public class SyncResultDto
{
    public bool Success { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Create ProfileData.cs (internal scraper output type)**

```csharp
// LinkedInPortfolio.API/Services/ProfileData.cs
namespace LinkedInPortfolio.API.Services;

public class ProfileData
{
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string About { get; set; } = string.Empty;
    public string PhotoBase64 { get; set; } = string.Empty;
    public List<ExperienceData> Experiences { get; set; } = new();
    public List<EducationData> Educations { get; set; } = new();
    public List<SkillData> Skills { get; set; } = new();
    public List<ProjectData> Projects { get; set; } = new();
    public List<CertificationData> Certifications { get; set; } = new();
}

public class ExperienceData
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}

public class EducationData
{
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    public string StartYear { get; set; } = string.Empty;
    public string EndYear { get; set; } = string.Empty;
}

public class SkillData
{
    public string Name { get; set; } = string.Empty;
    public int EndorsementCount { get; set; }
}

public class ProjectData
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

public class CertificationData
{
    public string Name { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string CredentialUrl { get; set; } = string.Empty;
}
```

- [ ] **Step 5: Verify build**

```powershell
cd D:\Projects\LinkedInPortfolio && dotnet build
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6: Commit**

```powershell
git add LinkedInPortfolio.API/DTOs/ LinkedInPortfolio.API/Services/ProfileData.cs
git commit -m "feat: add DTOs and internal ProfileData transfer types"
```

---

## Task 5: Create Service Interfaces and Test Project

**Files:**
- Create: `LinkedInPortfolio.API/Services/IProfileService.cs`
- Create: `LinkedInPortfolio.API/Services/ILinkedInScraperService.cs`
- Create: `LinkedInPortfolio.Tests/LinkedInPortfolio.Tests.csproj`

- [ ] **Step 1: Create IProfileService.cs**

```csharp
// LinkedInPortfolio.API/Services/IProfileService.cs
using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public interface IProfileService
{
    Task<ProfileDto?> GetProfileAsync();
    Task<ProfileStatusDto> GetStatusAsync();
    Task SaveProfileAsync(ProfileData data);
}
```

- [ ] **Step 2: Create ILinkedInScraperService.cs**

```csharp
// LinkedInPortfolio.API/Services/ILinkedInScraperService.cs
namespace LinkedInPortfolio.API.Services;

public interface ILinkedInScraperService
{
    Task<ProfileData> ScrapeProfileAsync();
}
```

- [ ] **Step 3: Scaffold test project**

```powershell
cd D:\Projects\LinkedInPortfolio
dotnet new xunit -n LinkedInPortfolio.Tests
dotnet sln add LinkedInPortfolio.Tests/LinkedInPortfolio.Tests.csproj
cd LinkedInPortfolio.Tests
dotnet add reference ../LinkedInPortfolio.API/LinkedInPortfolio.API.csproj
dotnet add package Moq --version 4.20.72
dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 8.0.0
```

- [ ] **Step 4: Remove the generated test file**

```powershell
Remove-Item D:\Projects\LinkedInPortfolio\LinkedInPortfolio.Tests\UnitTest1.cs
```

- [ ] **Step 5: Verify build**

```powershell
cd D:\Projects\LinkedInPortfolio && dotnet build
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6: Commit**

```powershell
git add LinkedInPortfolio.API/Services/IProfileService.cs LinkedInPortfolio.API/Services/ILinkedInScraperService.cs LinkedInPortfolio.Tests/
git commit -m "feat: add service interfaces and test project"
```

---

## Task 6: Write ProfileService Tests (Failing)

**Files:**
- Create: `LinkedInPortfolio.Tests/ProfileServiceTests.cs`

- [ ] **Step 1: Create ProfileServiceTests.cs**

```csharp
// LinkedInPortfolio.Tests/ProfileServiceTests.cs
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
```

- [ ] **Step 2: Run tests and confirm they fail**

```powershell
cd D:\Projects\LinkedInPortfolio
dotnet test LinkedInPortfolio.Tests --no-build 2>&1 | Select-String -Pattern "error|failed|passed" | head -5
```

Expected: Build error — `ProfileService` type not found. That is correct at this stage.

- [ ] **Step 3: Commit failing tests**

```powershell
git add LinkedInPortfolio.Tests/ProfileServiceTests.cs
git commit -m "test: add failing ProfileService tests"
```

---

## Task 7: Implement ProfileService (Make Tests Pass)

**Files:**
- Create: `LinkedInPortfolio.API/Services/ProfileService.cs`

- [ ] **Step 1: Create ProfileService.cs**

```csharp
// LinkedInPortfolio.API/Services/ProfileService.cs
using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInPortfolio.API.Services;

public class ProfileService(AppDbContext db) : IProfileService
{
    public async Task<ProfileDto?> GetProfileAsync()
    {
        var snapshot = await db.ProfileSnapshots
            .Include(p => p.Experiences)
            .Include(p => p.Educations)
            .Include(p => p.Skills)
            .Include(p => p.Projects)
            .Include(p => p.Certifications)
            .OrderByDescending(p => p.FetchedAt)
            .FirstOrDefaultAsync();

        if (snapshot is null) return null;

        return new ProfileDto
        {
            FetchedAt = snapshot.FetchedAt,
            Name = snapshot.Name,
            Headline = snapshot.Headline,
            Location = snapshot.Location,
            About = snapshot.About,
            PhotoBase64 = snapshot.PhotoBase64,
            Experience = snapshot.Experiences.Select(e => new ExperienceDto
            {
                Title = e.Title,
                Company = e.Company,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Description = e.Description,
                IsCurrent = e.IsCurrent
            }).ToList(),
            Education = snapshot.Educations.Select(e => new EducationDto
            {
                School = e.School,
                Degree = e.Degree,
                FieldOfStudy = e.FieldOfStudy,
                StartYear = e.StartYear,
                EndYear = e.EndYear
            }).ToList(),
            Skills = snapshot.Skills.Select(s => new SkillDto
            {
                Name = s.Name,
                EndorsementCount = s.EndorsementCount
            }).ToList(),
            Projects = snapshot.Projects.Select(p => new ProjectDto
            {
                Title = p.Title,
                Description = p.Description,
                Url = p.Url,
                StartDate = p.StartDate,
                EndDate = p.EndDate
            }).ToList(),
            Certifications = snapshot.Certifications.Select(c => new CertificationDto
            {
                Name = c.Name,
                IssuingOrganization = c.IssuingOrganization,
                IssueDate = c.IssueDate,
                CredentialUrl = c.CredentialUrl
            }).ToList()
        };
    }

    public async Task<ProfileStatusDto> GetStatusAsync()
    {
        var snapshot = await db.ProfileSnapshots
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

    public async Task SaveProfileAsync(ProfileData data)
    {
        var existing = await db.ProfileSnapshots.ToListAsync();
        db.ProfileSnapshots.RemoveRange(existing);
        await db.SaveChangesAsync();

        var snapshot = new ProfileSnapshot
        {
            FetchedAt = data.FetchedAt,
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
}
```

- [ ] **Step 2: Run tests and confirm they pass**

```powershell
cd D:\Projects\LinkedInPortfolio
dotnet test LinkedInPortfolio.Tests -v normal
```

Expected:
```
Passed!  - Failed: 0, Passed: 5, Skipped: 0
```

- [ ] **Step 3: Commit**

```powershell
git add LinkedInPortfolio.API/Services/ProfileService.cs
git commit -m "feat: implement ProfileService with full CRUD and mapping"
```

---

## Task 8: Write Controller Tests (Failing)

**Files:**
- Create: `LinkedInPortfolio.Tests/SyncControllerTests.cs`

- [ ] **Step 1: Create SyncControllerTests.cs**

```csharp
// LinkedInPortfolio.Tests/SyncControllerTests.cs
using LinkedInPortfolio.API.Controllers;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LinkedInPortfolio.Tests;

public class SyncControllerTests
{
    [Fact]
    public async Task Sync_WhenScraperSucceeds_Returns200WithSuccessTrue()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();
        var fetchedAt = DateTime.UtcNow;

        mockScraper.Setup(s => s.ScrapeProfileAsync())
            .ReturnsAsync(new ProfileData { Name = "Ahmed", FetchedAt = fetchedAt });

        mockProfileService.Setup(s => s.SaveProfileAsync(It.IsAny<ProfileData>()))
            .Returns(Task.CompletedTask);

        var controller = new SyncController(mockScraper.Object, mockProfileService.Object);
        var result = await controller.Sync();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SyncResultDto>(ok.Value);
        Assert.True(dto.Success);
        Assert.Equal(fetchedAt, dto.SyncedAt);
        Assert.Equal("Profile synced successfully", dto.Message);
    }

    [Fact]
    public async Task Sync_WhenScraperThrows_Returns500WithSuccessFalse()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();

        mockScraper.Setup(s => s.ScrapeProfileAsync())
            .ThrowsAsync(new InvalidOperationException("Login blocked"));

        var controller = new SyncController(mockScraper.Object, mockProfileService.Object);
        var result = await controller.Sync();

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var dto = Assert.IsType<SyncResultDto>(statusResult.Value);
        Assert.False(dto.Success);
        Assert.Contains("Login blocked", dto.Message);
    }

    [Fact]
    public async Task Sync_WhenSucceeds_CallsSaveProfileAsync()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();
        var profileData = new ProfileData { Name = "Ahmed", FetchedAt = DateTime.UtcNow };

        mockScraper.Setup(s => s.ScrapeProfileAsync()).ReturnsAsync(profileData);
        mockProfileService.Setup(s => s.SaveProfileAsync(It.IsAny<ProfileData>())).Returns(Task.CompletedTask);

        var controller = new SyncController(mockScraper.Object, mockProfileService.Object);
        await controller.Sync();

        mockProfileService.Verify(s => s.SaveProfileAsync(profileData), Times.Once);
    }
}
```

- [ ] **Step 2: Run tests and confirm build error**

```powershell
cd D:\Projects\LinkedInPortfolio
dotnet build LinkedInPortfolio.Tests 2>&1 | Select-String "error"
```

Expected: Build error — `SyncController` type not found. Correct at this stage.

- [ ] **Step 3: Commit**

```powershell
git add LinkedInPortfolio.Tests/SyncControllerTests.cs
git commit -m "test: add failing SyncController tests"
```

---

## Task 9: Implement Controllers

**Files:**
- Create: `LinkedInPortfolio.API/Controllers/ProfileController.cs`
- Create: `LinkedInPortfolio.API/Controllers/SyncController.cs`

- [ ] **Step 1: Create ProfileController.cs**

```csharp
// LinkedInPortfolio.API/Controllers/ProfileController.cs
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInPortfolio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var profile = await profileService.GetProfileAsync();
        if (profile is null) return NoContent();
        return Ok(profile);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await profileService.GetStatusAsync();
        return Ok(status);
    }
}
```

- [ ] **Step 2: Create SyncController.cs**

```csharp
// LinkedInPortfolio.API/Controllers/SyncController.cs
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInPortfolio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController(ILinkedInScraperService scraper, IProfileService profileService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Sync()
    {
        try
        {
            var data = await scraper.ScrapeProfileAsync();
            await profileService.SaveProfileAsync(data);
            return Ok(new SyncResultDto
            {
                Success = true,
                SyncedAt = data.FetchedAt,
                Message = "Profile synced successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new SyncResultDto
            {
                Success = false,
                Message = $"Sync failed: {ex.Message}"
            });
        }
    }
}
```

- [ ] **Step 3: Run all tests and confirm they pass**

```powershell
cd D:\Projects\LinkedInPortfolio
dotnet test -v normal
```

Expected:
```
Passed!  - Failed: 0, Passed: 8, Skipped: 0
```

- [ ] **Step 4: Commit**

```powershell
git add LinkedInPortfolio.API/Controllers/
git commit -m "feat: add ProfileController and SyncController"
```

---

## Task 10: Wire Up Program.cs

**Files:**
- Modify: `LinkedInPortfolio.API/Program.cs`

- [ ] **Step 1: Replace Program.cs**

```csharp
// LinkedInPortfolio.API/Program.cs
using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ILinkedInScraperService, LinkedInScraperService>();
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
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

- [ ] **Step 2: Create a stub LinkedInScraperService so the project compiles**

The real implementation comes in Task 12. For now, create a stub so Program.cs can wire DI.

```csharp
// LinkedInPortfolio.API/Services/LinkedInScraperService.cs
namespace LinkedInPortfolio.API.Services;

public class LinkedInScraperService : ILinkedInScraperService
{
    public Task<ProfileData> ScrapeProfileAsync()
        => throw new NotImplementedException("Scraper not yet implemented");
}
```

- [ ] **Step 3: Verify build**

```powershell
cd D:\Projects\LinkedInPortfolio && dotnet build
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4: Commit**

```powershell
git add LinkedInPortfolio.API/Program.cs LinkedInPortfolio.API/Services/LinkedInScraperService.cs
git commit -m "feat: wire DI, CORS, and auto-migration in Program.cs"
```

---

## Task 11: Create EF Migration and Apply to Database

**Files:**
- Create: `LinkedInPortfolio.API/Data/Migrations/` (generated)

- [ ] **Step 1: Install dotnet-ef global tool (if not already installed)**

```powershell
dotnet tool install --global dotnet-ef
```

If already installed, this will print a warning — that's fine.

- [ ] **Step 2: Create initial migration**

```powershell
cd D:\Projects\LinkedInPortfolio\LinkedInPortfolio.API
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
```

Expected: Migration file created at `Data/Migrations/YYYYMMDD_InitialCreate.cs`.

- [ ] **Step 2: Apply migration to LocalDB**

```powershell
dotnet ef database update
```

Expected:
```
Applying migration '..._InitialCreate'.
Done.
```

- [ ] **Step 3: Verify tables were created**

```powershell
dotnet ef dbcontext info
```

Expected: Shows connection string pointing to `(localdb)\mssqllocaldb`.

- [ ] **Step 4: Run the API and verify it starts**

```powershell
cd D:\Projects\LinkedInPortfolio\LinkedInPortfolio.API
dotnet run &
Start-Sleep -Seconds 3
Invoke-RestMethod -Uri "http://localhost:5000/api/profile/status" -Method GET | ConvertTo-Json
```

Expected: Returns `{"lastSyncedAt":null,"experienceCount":0,"educationCount":0,"skillCount":0,"projectCount":0,"certificationCount":0}`.

Stop the running API with Ctrl+C after verifying.

- [ ] **Step 5: Commit**

```powershell
git add LinkedInPortfolio.API/Data/Migrations/
git commit -m "feat: add EF Core initial migration"
```

---

## Task 12: Implement LinkedInScraperService

**Files:**
- Modify: `LinkedInPortfolio.API/Services/LinkedInScraperService.cs`

- [ ] **Step 1: Replace LinkedInScraperService.cs with full implementation**

```csharp
// LinkedInPortfolio.API/Services/LinkedInScraperService.cs
using PuppeteerSharp;

namespace LinkedInPortfolio.API.Services;

public class LinkedInScraperService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<LinkedInScraperService> logger) : ILinkedInScraperService
{
    public async Task<ProfileData> ScrapeProfileAsync()
    {
        var email = config["LinkedIn:Email"] ?? throw new InvalidOperationException("LinkedIn:Email not configured");
        var password = config["LinkedIn:Password"] ?? throw new InvalidOperationException("LinkedIn:Password not configured");
        var slug = config["LinkedIn:ProfileSlug"] ?? throw new InvalidOperationException("LinkedIn:ProfileSlug not configured");

        logger.LogInformation("Downloading Chromium if needed...");
        await new BrowserFetcher().DownloadAsync();

        logger.LogInformation("Launching browser...");
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage", "--disable-gpu"]
        });

        await using var page = await browser.NewPageAsync();
        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
        await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 900 });

        logger.LogInformation("Logging in to LinkedIn...");
        await page.GoToAsync("https://www.linkedin.com/login", new NavigationOptions { WaitUntil = [WaitUntilNavigation.NetworkIdle] });
        await Delay();

        await page.TypeAsync("#username", email, new TypeOptions { Delay = 80 });
        await Delay(300, 600);
        await page.TypeAsync("#password", password, new TypeOptions { Delay = 80 });
        await Delay(400, 800);
        await page.ClickAsync("[data-litms-control-urn='login-submit'], [type='submit']");
        await page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = [WaitUntilNavigation.NetworkIdle], Timeout = 15000 });
        await Delay(2000, 3500);

        var currentUrl = page.Url;
        if (currentUrl.Contains("checkpoint") || currentUrl.Contains("login"))
            throw new InvalidOperationException($"LinkedIn login failed or security checkpoint triggered. URL: {currentUrl}. Check credentials or solve the CAPTCHA manually first.");

        logger.LogInformation("Navigating to profile {Slug}...", slug);
        await page.GoToAsync($"https://www.linkedin.com/in/{slug}/", new NavigationOptions { WaitUntil = [WaitUntilNavigation.NetworkIdle] });
        await Delay(2000, 3000);

        await ScrollPageFully(page);
        await ExpandSections(page);

        logger.LogInformation("Extracting profile data...");
        return await ExtractAll(page);
    }

    private static async Task ScrollPageFully(IPage page)
    {
        for (int i = 0; i < 6; i++)
        {
            await page.EvaluateExpressionAsync("window.scrollBy(0, window.innerHeight * 1.5)");
            await Task.Delay(700);
        }
        await page.EvaluateExpressionAsync("window.scrollTo(0, 0)");
        await Task.Delay(1000);
    }

    private static async Task ExpandSections(IPage page)
    {
        // Click all "show more" / "see more" buttons
        var buttons = await page.QuerySelectorAllAsync("button.inline-show-more-text__button, button[aria-label*='see more'], button[aria-label*='Show more']");
        foreach (var btn in buttons)
        {
            try { await btn.ClickAsync(); await Task.Delay(200); } catch { /* ignore */ }
        }
        await Task.Delay(500);
    }

    private async Task<ProfileData> ExtractAll(IPage page)
    {
        var name = await GetText(page, "h1");
        var headline = await GetText(page, ".text-body-medium.break-words");
        var location = await GetText(page, ".text-body-small.inline.t-black--light.break-words");
        var about = await GetAbout(page);
        var photoBase64 = await GetPhoto(page);
        var experiences = await ExtractExperiences(page);
        var educations = await ExtractEducations(page);
        var skills = await ExtractSkills(page);
        var projects = await ExtractProjects(page);
        var certifications = await ExtractCertifications(page);

        return new ProfileData
        {
            FetchedAt = DateTime.UtcNow,
            Name = name,
            Headline = headline,
            Location = location,
            About = about,
            PhotoBase64 = photoBase64,
            Experiences = experiences,
            Educations = educations,
            Skills = skills,
            Projects = projects,
            Certifications = certifications
        };
    }

    private static async Task<string> GetText(IPage page, string selector)
    {
        try
        {
            var el = await page.QuerySelectorAsync(selector);
            if (el is null) return string.Empty;
            var prop = await el.GetPropertyAsync("innerText");
            return prop?.RemoteObject?.Value?.ToString()?.Trim() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static async Task<string> GetAbout(IPage page)
    {
        try
        {
            var section = await page.QuerySelectorAsync("#about");
            if (section is null) return string.Empty;
            // LinkedIn about text is in a sibling container
            var text = await page.EvaluateExpressionAsync<string>(@"
                (() => {
                    const heading = document.querySelector('#about');
                    if (!heading) return '';
                    let el = heading.parentElement;
                    while (el) {
                        const spans = el.querySelectorAll('span[aria-hidden=true]');
                        const text = Array.from(spans).map(s => s.textContent?.trim()).filter(Boolean).join(' ');
                        if (text.length > 20) return text;
                        el = el.nextElementSibling;
                        if (!el) break;
                    }
                    return '';
                })()
            ");
            return text ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private async Task<string> GetPhoto(IPage page)
    {
        try
        {
            var img = await page.QuerySelectorAsync("img.pv-top-card-profile-picture__image--show, .profile-photo-edit__preview, img[class*='profile-photo']");
            if (img is null) return string.Empty;
            var srcProp = await img.GetPropertyAsync("src");
            var src = srcProp?.RemoteObject?.Value?.ToString();
            if (string.IsNullOrEmpty(src) || src.StartsWith("data:")) return src ?? string.Empty;

            var cookies = await page.GetCookiesAsync("https://www.linkedin.com");
            var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));

            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
            client.DefaultRequestHeaders.Add("Referer", "https://www.linkedin.com/");
            var bytes = await client.GetByteArrayAsync(src);
            return Convert.ToBase64String(bytes);
        }
        catch { return string.Empty; }
    }

    private static async Task<List<ExperienceData>> ExtractExperiences(IPage page)
    {
        try
        {
            var json = await page.EvaluateExpressionAsync<string>(@"
                JSON.stringify((() => {
                    const heading = document.querySelector('#experience');
                    if (!heading) return [];
                    const section = heading.closest('section') || heading.parentElement?.parentElement?.parentElement;
                    if (!section) return [];
                    const items = section.querySelectorAll('li.artdeco-list__item');
                    return Array.from(items).map(item => {
                        const spans = Array.from(item.querySelectorAll('span[aria-hidden=true]')).map(s => s.textContent?.trim()).filter(s => s && s.length > 0);
                        const dateRange = spans.find(s => s.includes('–') || s.includes('-') || /^\d{4}/.test(s)) || '';
                        const parts = dateRange.split(/\s*[–-]\s*/);
                        return {
                            Title: spans[0] || '',
                            Company: spans[1] || '',
                            StartDate: parts[0] || '',
                            EndDate: parts[1] || '',
                            Description: spans[5] || '',
                            IsCurrent: dateRange.toLowerCase().includes('present')
                        };
                    }).filter(e => e.Title.length > 0);
                })())
            ");
            return string.IsNullOrEmpty(json) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<ExperienceData>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { return []; }
    }

    private static async Task<List<EducationData>> ExtractEducations(IPage page)
    {
        try
        {
            var json = await page.EvaluateExpressionAsync<string>(@"
                JSON.stringify((() => {
                    const heading = document.querySelector('#education');
                    if (!heading) return [];
                    const section = heading.closest('section') || heading.parentElement?.parentElement?.parentElement;
                    if (!section) return [];
                    const items = section.querySelectorAll('li.artdeco-list__item');
                    return Array.from(items).map(item => {
                        const spans = Array.from(item.querySelectorAll('span[aria-hidden=true]')).map(s => s.textContent?.trim()).filter(s => s && s.length > 0);
                        const yearSpan = spans.find(s => /\d{4}/.test(s)) || '';
                        const years = yearSpan.match(/\d{4}/g) || [];
                        return {
                            School: spans[0] || '',
                            Degree: spans[1] || '',
                            FieldOfStudy: spans[2] || '',
                            StartYear: years[0] || '',
                            EndYear: years[1] || ''
                        };
                    }).filter(e => e.School.length > 0);
                })())
            ");
            return string.IsNullOrEmpty(json) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<EducationData>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { return []; }
    }

    private static async Task<List<SkillData>> ExtractSkills(IPage page)
    {
        try
        {
            var json = await page.EvaluateExpressionAsync<string>(@"
                JSON.stringify((() => {
                    const heading = document.querySelector('#skills');
                    if (!heading) return [];
                    const section = heading.closest('section') || heading.parentElement?.parentElement?.parentElement;
                    if (!section) return [];
                    const items = section.querySelectorAll('li.artdeco-list__item');
                    return Array.from(items).map(item => {
                        const spans = Array.from(item.querySelectorAll('span[aria-hidden=true]')).map(s => s.textContent?.trim()).filter(s => s && s.length > 0);
                        const endorsementText = spans.find(s => /\d+/.test(s) && s.toLowerCase().includes('endorsement'));
                        const count = endorsementText ? parseInt(endorsementText.match(/\d+/)?.[0] || '0') : 0;
                        return { Name: spans[0] || '', EndorsementCount: count };
                    }).filter(s => s.Name.length > 0);
                })())
            ");
            return string.IsNullOrEmpty(json) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<SkillData>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { return []; }
    }

    private static async Task<List<ProjectData>> ExtractProjects(IPage page)
    {
        try
        {
            var json = await page.EvaluateExpressionAsync<string>(@"
                JSON.stringify((() => {
                    const heading = document.querySelector('#projects');
                    if (!heading) return [];
                    const section = heading.closest('section') || heading.parentElement?.parentElement?.parentElement;
                    if (!section) return [];
                    const items = section.querySelectorAll('li.artdeco-list__item');
                    return Array.from(items).map(item => {
                        const spans = Array.from(item.querySelectorAll('span[aria-hidden=true]')).map(s => s.textContent?.trim()).filter(s => s && s.length > 0);
                        const link = item.querySelector('a[href]')?.getAttribute('href') || '';
                        const dateSpan = spans.find(s => /\d{4}/.test(s) || s.includes('–')) || '';
                        const parts = dateSpan.split(/\s*[–-]\s*/);
                        return {
                            Title: spans[0] || '',
                            Description: spans[2] || '',
                            Url: link,
                            StartDate: parts[0] || '',
                            EndDate: parts[1] || ''
                        };
                    }).filter(p => p.Title.length > 0);
                })())
            ");
            return string.IsNullOrEmpty(json) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<ProjectData>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { return []; }
    }

    private static async Task<List<CertificationData>> ExtractCertifications(IPage page)
    {
        try
        {
            var json = await page.EvaluateExpressionAsync<string>(@"
                JSON.stringify((() => {
                    const heading = document.querySelector('#licenses_and_certifications, #certifications');
                    if (!heading) return [];
                    const section = heading.closest('section') || heading.parentElement?.parentElement?.parentElement;
                    if (!section) return [];
                    const items = section.querySelectorAll('li.artdeco-list__item');
                    return Array.from(items).map(item => {
                        const spans = Array.from(item.querySelectorAll('span[aria-hidden=true]')).map(s => s.textContent?.trim()).filter(s => s && s.length > 0);
                        const link = item.querySelector('a[href]')?.getAttribute('href') || '';
                        return {
                            Name: spans[0] || '',
                            IssuingOrganization: spans[1] || '',
                            IssueDate: spans[2] || '',
                            CredentialUrl: link
                        };
                    }).filter(c => c.Name.length > 0);
                })())
            ");
            return string.IsNullOrEmpty(json) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<CertificationData>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { return []; }
    }

    private static Task Delay(int minMs = 800, int maxMs = 1800)
        => Task.Delay(Random.Shared.Next(minMs, maxMs));
}
```

- [ ] **Step 2: Verify build**

```powershell
cd D:\Projects\LinkedInPortfolio && dotnet build
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3: Run all tests (they should still pass)**

```powershell
dotnet test -v normal
```

Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0`

- [ ] **Step 4: Update appsettings.json with real LinkedIn credentials**

Edit `LinkedInPortfolio.API/appsettings.json`:
- Set `LinkedIn:Email` to your actual LinkedIn email
- Set `LinkedIn:Password` to your actual LinkedIn password
- Set `LinkedIn:ProfileSlug` to your profile slug (e.g., `ahmed-omran-123`)

> **Note:** LinkedIn selectors change periodically. If scraping returns empty sections, inspect the live page in Chrome DevTools and update the CSS selectors in `ExtractExperiences`, `ExtractEducations`, etc.

- [ ] **Step 5: Commit**

```powershell
git add LinkedInPortfolio.API/Services/LinkedInScraperService.cs
git commit -m "feat: implement LinkedInScraperService with PuppeteerSharp"
```

---

## Task 13: Scaffold React App

**Files:**
- Create: `linkedin-portfolio-ui/` (entire Vite project)

- [ ] **Step 1: Scaffold Vite React TypeScript app**

```powershell
cd D:\Projects\LinkedInPortfolio
npm create vite@latest linkedin-portfolio-ui -- --template react-ts
cd linkedin-portfolio-ui
npm install
```

- [ ] **Step 2: Install dependencies**

```powershell
npm install @tanstack/react-query react-router-dom axios
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
```

- [ ] **Step 3: Configure Tailwind — replace tailwind.config.js**

```js
// linkedin-portfolio-ui/tailwind.config.js
/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: { extend: {} },
  plugins: [],
}
```

- [ ] **Step 4: Replace src/index.css with Tailwind directives**

```css
/* linkedin-portfolio-ui/src/index.css */
@tailwind base;
@tailwind components;
@tailwind utilities;
```

- [ ] **Step 5: Configure Vite proxy — replace vite.config.ts**

```ts
// linkedin-portfolio-ui/vite.config.ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      }
    }
  }
})
```

- [ ] **Step 6: Delete generated boilerplate**

```powershell
Remove-Item src/App.css
Remove-Item src/assets/react.svg
Remove-Item public/vite.svg
```

- [ ] **Step 7: Verify dev server starts**

```powershell
npm run dev
```

Expected: Vite dev server starts at `http://localhost:5173`. Stop it with Ctrl+C.

- [ ] **Step 8: Commit**

```powershell
cd D:\Projects\LinkedInPortfolio
git add linkedin-portfolio-ui/
git commit -m "feat: scaffold React app with Vite, TypeScript, and TailwindCSS"
```

---

## Task 14: Create TypeScript Types and API Client

**Files:**
- Create: `linkedin-portfolio-ui/src/types/profile.ts`
- Create: `linkedin-portfolio-ui/src/api/profileApi.ts`

- [ ] **Step 1: Create profile.ts**

```ts
// linkedin-portfolio-ui/src/types/profile.ts
export interface ExperienceDto {
  title: string
  company: string
  startDate: string
  endDate: string
  description: string
  isCurrent: boolean
}

export interface EducationDto {
  school: string
  degree: string
  fieldOfStudy: string
  startYear: string
  endYear: string
}

export interface SkillDto {
  name: string
  endorsementCount: number
}

export interface ProjectDto {
  title: string
  description: string
  url: string
  startDate: string
  endDate: string
}

export interface CertificationDto {
  name: string
  issuingOrganization: string
  issueDate: string
  credentialUrl: string
}

export interface ProfileDto {
  fetchedAt: string
  name: string
  headline: string
  location: string
  about: string
  photoBase64: string
  experience: ExperienceDto[]
  education: EducationDto[]
  skills: SkillDto[]
  projects: ProjectDto[]
  certifications: CertificationDto[]
}

export interface ProfileStatusDto {
  lastSyncedAt: string | null
  experienceCount: number
  educationCount: number
  skillCount: number
  projectCount: number
  certificationCount: number
}

export interface SyncResultDto {
  success: boolean
  syncedAt: string | null
  message: string
}
```

- [ ] **Step 2: Create profileApi.ts**

```ts
// linkedin-portfolio-ui/src/api/profileApi.ts
import axios from 'axios'
import type { ProfileDto, ProfileStatusDto, SyncResultDto } from '../types/profile'

const api = axios.create({ baseURL: '/api' })

export async function fetchProfile(): Promise<ProfileDto | null> {
  const res = await api.get<ProfileDto>('/profile')
  if (res.status === 204) return null
  return res.data
}

export async function fetchStatus(): Promise<ProfileStatusDto> {
  const res = await api.get<ProfileStatusDto>('/profile/status')
  return res.data
}

export async function triggerSync(): Promise<SyncResultDto> {
  const res = await api.post<SyncResultDto>('/sync')
  return res.data
}
```

- [ ] **Step 3: Commit**

```powershell
cd D:\Projects\LinkedInPortfolio
git add linkedin-portfolio-ui/src/types/ linkedin-portfolio-ui/src/api/
git commit -m "feat: add TypeScript types and API client"
```

---

## Task 15: Create UI Components

**Files:**
- Create: `linkedin-portfolio-ui/src/components/ProfileHeader.tsx`
- Create: `linkedin-portfolio-ui/src/components/AboutSection.tsx`
- Create: `linkedin-portfolio-ui/src/components/ExperienceSection.tsx`
- Create: `linkedin-portfolio-ui/src/components/EducationSection.tsx`
- Create: `linkedin-portfolio-ui/src/components/SkillsSection.tsx`
- Create: `linkedin-portfolio-ui/src/components/ProjectsSection.tsx`
- Create: `linkedin-portfolio-ui/src/components/CertificationsSection.tsx`
- Create: `linkedin-portfolio-ui/src/components/LoadingSpinner.tsx`

- [ ] **Step 1: Create ProfileHeader.tsx**

```tsx
// linkedin-portfolio-ui/src/components/ProfileHeader.tsx
import type { ProfileDto } from '../types/profile'

export function ProfileHeader({ name, headline, location, photoBase64 }: Pick<ProfileDto, 'name' | 'headline' | 'location' | 'photoBase64'>) {
  return (
    <div className="flex items-center gap-6 bg-white rounded-2xl shadow p-8">
      {photoBase64 ? (
        <img
          src={`data:image/jpeg;base64,${photoBase64}`}
          alt={name}
          className="w-32 h-32 rounded-full object-cover ring-4 ring-blue-100 shrink-0"
        />
      ) : (
        <div className="w-32 h-32 rounded-full bg-blue-100 flex items-center justify-center text-4xl font-bold text-blue-400 shrink-0">
          {name.charAt(0)}
        </div>
      )}
      <div>
        <h1 className="text-3xl font-bold text-gray-900">{name}</h1>
        <p className="text-lg text-gray-600 mt-1">{headline}</p>
        {location && <p className="text-sm text-gray-500 mt-1">📍 {location}</p>}
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Create AboutSection.tsx**

```tsx
// linkedin-portfolio-ui/src/components/AboutSection.tsx
interface Props { about: string }

export function AboutSection({ about }: Props) {
  if (!about) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-3">About</h2>
      <p className="text-gray-700 whitespace-pre-line leading-relaxed">{about}</p>
    </section>
  )
}
```

- [ ] **Step 3: Create ExperienceSection.tsx**

```tsx
// linkedin-portfolio-ui/src/components/ExperienceSection.tsx
import type { ExperienceDto } from '../types/profile'

interface Props { experience: ExperienceDto[] }

export function ExperienceSection({ experience }: Props) {
  if (!experience.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Experience</h2>
      <div className="space-y-6">
        {experience.map((e, i) => (
          <div key={i} className="border-l-2 border-blue-200 pl-4">
            <div className="flex items-start justify-between">
              <div>
                <h3 className="font-semibold text-gray-900">{e.title}</h3>
                <p className="text-blue-600 text-sm">{e.company}</p>
              </div>
              <span className="text-xs text-gray-500 whitespace-nowrap ml-4">
                {e.startDate}{e.isCurrent ? ' – Present' : e.endDate ? ` – ${e.endDate}` : ''}
              </span>
            </div>
            {e.description && <p className="text-gray-600 text-sm mt-2">{e.description}</p>}
          </div>
        ))}
      </div>
    </section>
  )
}
```

- [ ] **Step 4: Create EducationSection.tsx**

```tsx
// linkedin-portfolio-ui/src/components/EducationSection.tsx
import type { EducationDto } from '../types/profile'

interface Props { education: EducationDto[] }

export function EducationSection({ education }: Props) {
  if (!education.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Education</h2>
      <div className="space-y-4">
        {education.map((e, i) => (
          <div key={i} className="border-l-2 border-green-200 pl-4">
            <h3 className="font-semibold text-gray-900">{e.school}</h3>
            <p className="text-sm text-gray-600">{[e.degree, e.fieldOfStudy].filter(Boolean).join(' · ')}</p>
            {(e.startYear || e.endYear) && (
              <p className="text-xs text-gray-500">{e.startYear}{e.endYear ? ` – ${e.endYear}` : ''}</p>
            )}
          </div>
        ))}
      </div>
    </section>
  )
}
```

- [ ] **Step 5: Create SkillsSection.tsx**

```tsx
// linkedin-portfolio-ui/src/components/SkillsSection.tsx
import type { SkillDto } from '../types/profile'

interface Props { skills: SkillDto[] }

export function SkillsSection({ skills }: Props) {
  if (!skills.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Skills</h2>
      <div className="flex flex-wrap gap-2">
        {skills.map((s, i) => (
          <span key={i} className="px-3 py-1 bg-blue-50 text-blue-700 rounded-full text-sm font-medium">
            {s.name}{s.endorsementCount > 0 ? ` · ${s.endorsementCount}` : ''}
          </span>
        ))}
      </div>
    </section>
  )
}
```

- [ ] **Step 6: Create ProjectsSection.tsx**

```tsx
// linkedin-portfolio-ui/src/components/ProjectsSection.tsx
import type { ProjectDto } from '../types/profile'

interface Props { projects: ProjectDto[] }

export function ProjectsSection({ projects }: Props) {
  if (!projects.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Projects</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {projects.map((p, i) => (
          <div key={i} className="border border-gray-100 rounded-xl p-4 hover:shadow-md transition-shadow">
            <div className="flex items-start justify-between">
              <h3 className="font-semibold text-gray-900">{p.title}</h3>
              {p.url && (
                <a href={p.url} target="_blank" rel="noopener noreferrer" className="text-blue-500 text-xs ml-2 shrink-0">
                  ↗ Link
                </a>
              )}
            </div>
            {(p.startDate || p.endDate) && (
              <p className="text-xs text-gray-500 mt-1">{p.startDate}{p.endDate ? ` – ${p.endDate}` : ''}</p>
            )}
            {p.description && <p className="text-sm text-gray-600 mt-2">{p.description}</p>}
          </div>
        ))}
      </div>
    </section>
  )
}
```

- [ ] **Step 7: Create CertificationsSection.tsx**

```tsx
// linkedin-portfolio-ui/src/components/CertificationsSection.tsx
import type { CertificationDto } from '../types/profile'

interface Props { certifications: CertificationDto[] }

export function CertificationsSection({ certifications }: Props) {
  if (!certifications.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Certifications</h2>
      <div className="space-y-3">
        {certifications.map((c, i) => (
          <div key={i} className="flex items-start gap-3">
            <div className="w-2 h-2 rounded-full bg-purple-400 mt-2 shrink-0" />
            <div>
              <p className="font-medium text-gray-900">
                {c.credentialUrl ? (
                  <a href={c.credentialUrl} target="_blank" rel="noopener noreferrer" className="hover:text-blue-600">
                    {c.name}
                  </a>
                ) : c.name}
              </p>
              <p className="text-sm text-gray-500">{c.issuingOrganization}{c.issueDate ? ` · ${c.issueDate}` : ''}</p>
            </div>
          </div>
        ))}
      </div>
    </section>
  )
}
```

- [ ] **Step 8: Create LoadingSpinner.tsx**

```tsx
// linkedin-portfolio-ui/src/components/LoadingSpinner.tsx
interface Props { message?: string }

export function LoadingSpinner({ message = 'Loading...' }: Props) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-12">
      <div className="w-10 h-10 border-4 border-blue-200 border-t-blue-600 rounded-full animate-spin" />
      <p className="text-gray-500 text-sm">{message}</p>
    </div>
  )
}
```

- [ ] **Step 9: Commit**

```powershell
cd D:\Projects\LinkedInPortfolio
git add linkedin-portfolio-ui/src/components/
git commit -m "feat: add all portfolio UI components"
```

---

## Task 16: Create Pages and Wire Up Routing

**Files:**
- Create: `linkedin-portfolio-ui/src/pages/PortfolioPage.tsx`
- Create: `linkedin-portfolio-ui/src/pages/AdminPage.tsx`
- Modify: `linkedin-portfolio-ui/src/App.tsx`
- Modify: `linkedin-portfolio-ui/src/main.tsx`

- [ ] **Step 1: Create PortfolioPage.tsx**

```tsx
// linkedin-portfolio-ui/src/pages/PortfolioPage.tsx
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

export function PortfolioPage() {
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

  if (!profile) return (
    <div className="text-center py-20 text-gray-500">
      <p className="text-lg">No profile data yet.</p>
      <Link to="/admin" className="mt-3 inline-block text-blue-600 hover:underline">
        Go to Admin → Sync from LinkedIn
      </Link>
    </div>
  )

  return (
    <main className="max-w-3xl mx-auto px-4 py-8 space-y-6">
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
        Last synced: {new Date(profile.fetchedAt).toLocaleString()}
      </p>
    </main>
  )
}
```

- [ ] **Step 2: Create AdminPage.tsx**

```tsx
// linkedin-portfolio-ui/src/pages/AdminPage.tsx
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { fetchStatus, triggerSync } from '../api/profileApi'
import { LoadingSpinner } from '../components/LoadingSpinner'
import { Link } from 'react-router-dom'

export function AdminPage() {
  const queryClient = useQueryClient()
  const [syncMessage, setSyncMessage] = useState<{ text: string; success: boolean } | null>(null)

  const { data: status, isLoading: statusLoading } = useQuery({
    queryKey: ['status'],
    queryFn: fetchStatus,
  })

  const syncMutation = useMutation({
    mutationFn: triggerSync,
    onSuccess: (result) => {
      setSyncMessage({ text: result.message, success: result.success })
      if (result.success) {
        queryClient.invalidateQueries({ queryKey: ['profile'] })
        queryClient.invalidateQueries({ queryKey: ['status'] })
      }
    },
    onError: () => {
      setSyncMessage({ text: 'Unexpected error during sync.', success: false })
    },
  })

  return (
    <main className="max-w-lg mx-auto px-4 py-12">
      <div className="bg-white rounded-2xl shadow p-8 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900">LinkedIn Sync</h1>
          <Link to="/" className="text-blue-600 text-sm hover:underline">← Portfolio</Link>
        </div>

        {statusLoading ? (
          <LoadingSpinner message="Loading status..." />
        ) : (
          <div className="text-sm text-gray-600 space-y-1">
            <p><span className="font-medium">Last synced:</span> {status?.lastSyncedAt ? new Date(status.lastSyncedAt).toLocaleString() : 'Never'}</p>
            <p><span className="font-medium">Experience entries:</span> {status?.experienceCount ?? 0}</p>
            <p><span className="font-medium">Skills:</span> {status?.skillCount ?? 0}</p>
            <p><span className="font-medium">Projects:</span> {status?.projectCount ?? 0}</p>
            <p><span className="font-medium">Certifications:</span> {status?.certificationCount ?? 0}</p>
          </div>
        )}

        <button
          onClick={() => { setSyncMessage(null); syncMutation.mutate() }}
          disabled={syncMutation.isPending}
          className="w-full py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold rounded-xl transition-colors"
        >
          {syncMutation.isPending ? 'Syncing... (this takes 30–60s)' : 'Sync from LinkedIn'}
        </button>

        {syncMutation.isPending && <LoadingSpinner message="Scraping LinkedIn profile..." />}

        {syncMessage && (
          <div className={`p-4 rounded-xl text-sm ${syncMessage.success ? 'bg-green-50 text-green-800' : 'bg-red-50 text-red-800'}`}>
            {syncMessage.text}
          </div>
        )}
      </div>
    </main>
  )
}
```

- [ ] **Step 3: Replace App.tsx**

```tsx
// linkedin-portfolio-ui/src/App.tsx
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { PortfolioPage } from './pages/PortfolioPage'
import { AdminPage } from './pages/AdminPage'

const queryClient = new QueryClient()

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <div className="min-h-screen bg-gray-50">
          <Routes>
            <Route path="/" element={<PortfolioPage />} />
            <Route path="/admin" element={<AdminPage />} />
          </Routes>
        </div>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
```

- [ ] **Step 4: Replace main.tsx**

```tsx
// linkedin-portfolio-ui/src/main.tsx
import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.tsx'
import './index.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
```

- [ ] **Step 5: Verify TypeScript compiles**

```powershell
cd D:\Projects\LinkedInPortfolio\linkedin-portfolio-ui
npx tsc --noEmit
```

Expected: No TypeScript errors.

- [ ] **Step 6: Verify dev server starts and pages render**

```powershell
npm run dev
```

Open `http://localhost:5173` — should show "No profile data yet" empty state with link to /admin.
Open `http://localhost:5173/admin` — should show Sync page with status.
Stop with Ctrl+C.

- [ ] **Step 7: Commit**

```powershell
cd D:\Projects\LinkedInPortfolio
git add linkedin-portfolio-ui/src/
git commit -m "feat: add PortfolioPage, AdminPage, and React Router wiring"
```

---

## Task 17: End-to-End Smoke Test

- [ ] **Step 1: Start the API**

```powershell
cd D:\Projects\LinkedInPortfolio\LinkedInPortfolio.API
dotnet run
```

Leave this running. In a new terminal:

- [ ] **Step 2: Start the React app**

```powershell
cd D:\Projects\LinkedInPortfolio\linkedin-portfolio-ui
npm run dev
```

- [ ] **Step 3: Verify empty state**

Open `http://localhost:5173` — should display "No profile data yet."
Open `http://localhost:5173/admin` — should show "Last synced: Never" with Sync button.

- [ ] **Step 4: Verify status API**

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/profile/status" | ConvertTo-Json
```

Expected: `lastSyncedAt: null`, all counts 0.

- [ ] **Step 5: Update LinkedIn credentials and trigger sync**

- Ensure `appsettings.json` has your real LinkedIn email, password, and profile slug
- Click "Sync from LinkedIn" in the admin page
- Wait 30–60 seconds for PuppeteerSharp to complete

- [ ] **Step 6: Verify portfolio displays**

Navigate to `http://localhost:5173` — profile data should be displayed with all sections.

- [ ] **Step 7: Final commit**

```powershell
cd D:\Projects\LinkedInPortfolio
git add .
git commit -m "feat: complete LinkedIn portfolio app — scraper, API, and React frontend"
```

---

## Troubleshooting

**Login checkpoint / CAPTCHA:** Open the browser in non-headless mode by changing `Headless = false` in `LinkedInScraperService.cs`. Complete the CAPTCHA manually, then re-run in headless mode.

**Empty sections after sync:** LinkedIn changes their DOM selectors periodically. Open DevTools on your LinkedIn profile, inspect the section, and update the CSS selectors in `ExtractExperiences`, `ExtractEducations`, etc.

**Chromium download fails:** PuppeteerSharp downloads ~150MB on first run. Ensure internet access and disk space. The binary is cached in `%USERPROFILE%\.local-chromium`.

**CORS error in browser:** Verify the API is running on `http://localhost:5000` (not `https://localhost:5001`) and the Vite proxy target matches.
