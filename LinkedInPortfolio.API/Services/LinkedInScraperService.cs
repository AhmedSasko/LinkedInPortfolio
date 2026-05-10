// LinkedInPortfolio.API/Services/LinkedInScraperService.cs
using PuppeteerSharp;
using PuppeteerSharp.Input;

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
        await page.GoToAsync("https://www.linkedin.com/login", new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] });
        await Delay();

        await page.TypeAsync("#username", email, new TypeOptions { Delay = 80 });
        await Delay(300, 600);
        await page.TypeAsync("#password", password, new TypeOptions { Delay = 80 });
        await Delay(400, 800);
        await page.ClickAsync("[data-litms-control-urn='login-submit'], [type='submit']");
        await page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0], Timeout = 15000 });
        await Delay(2000, 3500);

        var currentUrl = page.Url;
        if (currentUrl.Contains("checkpoint") || currentUrl.Contains("login"))
            throw new InvalidOperationException($"LinkedIn login failed or security checkpoint triggered. URL: {currentUrl}. Check credentials or solve the CAPTCHA manually first.");

        logger.LogInformation("Navigating to profile {Slug}...", slug);
        await page.GoToAsync($"https://www.linkedin.com/in/{slug}/", new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] });
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
