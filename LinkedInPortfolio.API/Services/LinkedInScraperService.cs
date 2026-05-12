using System.Text.Json;
using PuppeteerSharp;

namespace LinkedInPortfolio.API.Services;

public class LinkedInScraperService(
    IHttpClientFactory httpClientFactory,
    ILogger<LinkedInScraperService> logger,
    IHostEnvironment env) : ILinkedInScraperService
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ProfileData> ScrapeProfileAsync(string profileUrl, string liAtCookie)
    {
        if (string.IsNullOrWhiteSpace(profileUrl))
            throw new ArgumentException("Profile URL cannot be empty.", nameof(profileUrl));
        if (string.IsNullOrWhiteSpace(liAtCookie))
            throw new ArgumentException("LinkedIn session cookie (li_at) cannot be empty.", nameof(liAtCookie));

        var executablePath = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
        logger.LogInformation("Launching headless browser{Path}...",
            string.IsNullOrEmpty(executablePath) ? "" : $" ({executablePath})");

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            DefaultViewport = null,
            ExecutablePath = string.IsNullOrEmpty(executablePath) ? null : executablePath,
            Args = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu"]
        });

        await using var page = await browser.NewPageAsync();
        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");

        // Step 1: Hit linkedin.com first so LinkedIn sets JSESSIONID + other session cookies
        logger.LogInformation("Establishing session cookies...");
        try
        {
            await page.GoToAsync("https://www.linkedin.com/", new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 30000 });
            await Task.Delay(1500);
        }
        catch (Exception ex) { logger.LogWarning("Initial navigation failed (non-fatal): {Msg}", ex.Message); }

        // Step 2: Inject li_at (and clear any login redirect from step 1)
        await page.SetCookieAsync(new CookieParam
        {
            Name = "li_at",
            Value = liAtCookie,
            Domain = ".linkedin.com",
            Path = "/",
            HttpOnly = true,
            Secure = true
        });

        // Step 3: Navigate to the profile to get authenticated page title + trigger JSESSIONID
        logger.LogInformation("Navigating to profile: {Url}", profileUrl);
        try
        {
            await page.GoToAsync(profileUrl, new NavigationOptions { WaitUntil = [WaitUntilNavigation.Load], Timeout = 60000 });
        }
        catch (Exception ex) when (ex.Message.Contains("ERR_TOO_MANY_REDIRECTS"))
        {
            throw new InvalidOperationException(
                "LinkedIn session cookie appears to be expired or invalid. Please get a fresh li_at cookie from your browser and try again.");
        }
        await Task.Delay(2000);

        // Auth check via URL
        var currentUrl = page.Url;
        logger.LogInformation("Current URL: {Url}", currentUrl);
        if (currentUrl.Contains("/login") || currentUrl.Contains("/authwall") || currentUrl.Contains("/uas/"))
            throw new InvalidOperationException(
                "LinkedIn session cookie appears to be expired or invalid. Please get a fresh li_at cookie from your browser and try again.");

        // Auth check via page title
        var pageTitle = await page.EvaluateExpressionAsync<string>("document.title ?? ''");
        logger.LogInformation("Page title: '{Title}'", pageTitle);
        if (string.IsNullOrWhiteSpace(pageTitle)
            || pageTitle.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase)
            || pageTitle.Contains("Sign In", StringComparison.OrdinalIgnoreCase)
            || pageTitle.Contains("Join LinkedIn", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "LinkedIn session cookie appears to be expired or invalid. Please get a fresh li_at cookie from your browser and try again.");

        // Step 4: Extract all cookies from the browser (Puppeteer API — no JS execution, no context issues)
        var cookies = await page.GetCookiesAsync("https://www.linkedin.com");
        var jsessionRaw = cookies.FirstOrDefault(c => c.Name == "JSESSIONID")?.Value ?? string.Empty;
        // JSESSIONID value may be quoted: "ajax:12345" → strip quotes
        var jsession = jsessionRaw.Trim('"');
        logger.LogInformation("JSESSIONID present: {Has}, li_at present: {HasLi}",
            !string.IsNullOrEmpty(jsession),
            cookies.Any(c => c.Name == "li_at"));

        // Debug screenshot
        if (env.IsDevelopment())
        {
            try
            {
                var shot = await page.ScreenshotDataAsync(new ScreenshotOptions { FullPage = false });
                await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "linkedin_profile.png"), shot);
                logger.LogInformation("Screenshot saved to /tmp/linkedin_profile.png");
            }
            catch (Exception ex) { logger.LogWarning("Screenshot failed: {Msg}", ex.Message); }
        }

        // Step 5: Call LinkedIn Voyager API directly from C# using the session cookies
        // This avoids the "execution context was destroyed" issue from in-page fetch() calls.
        var vanity = ExtractVanityName(profileUrl);
        var fallbackName = pageTitle.Replace("| LinkedIn", "").Trim().TrimEnd('|').Trim();

        if (string.IsNullOrEmpty(jsession))
        {
            logger.LogWarning("No JSESSIONID — cannot call Voyager API");
            return new ProfileData { FetchedAt = DateTime.UtcNow, Name = fallbackName };
        }

        logger.LogInformation("Calling Voyager API for vanity='{Vanity}'", vanity);
        var voyagerData = await CallVoyagerApi(liAtCookie, jsession, vanity);
        if (voyagerData is null)
        {
            logger.LogWarning("Voyager API returned no data — returning name only");
            return new ProfileData { FetchedAt = DateTime.UtcNow, Name = fallbackName };
        }

        if (!string.IsNullOrEmpty(voyagerData.Name))
            logger.LogInformation("Parsed: name='{N}' headline='{H}' exp={E} edu={Ed} skills={S}",
                voyagerData.Name, voyagerData.Headline,
                voyagerData.Experiences.Count, voyagerData.Educations.Count, voyagerData.Skills.Count);

        if (string.IsNullOrEmpty(voyagerData.Name))
            voyagerData.Name = fallbackName;

        return voyagerData;
    }

    private static string ExtractVanityName(string profileUrl)
    {
        var m = System.Text.RegularExpressions.Regex.Match(profileUrl, @"/in/([^/?#]+)");
        return m.Success ? m.Groups[1].Value.TrimEnd('/') : string.Empty;
    }

    private async Task<ProfileData?> CallVoyagerApi(string liAt, string jsession, string vanity)
    {
        // Try multiple decoration IDs (LinkedIn changes these between releases)
        string[] decorations =
        [
            "com.linkedin.voyager.dash.deco.identity.profile.FullProfileWithEntities-91",
            "com.linkedin.voyager.dash.deco.identity.profile.FullProfileWithEntities-86",
            "com.linkedin.voyager.dash.deco.identity.profile.FullProfileWithEntities-65",
        ];

        var client = httpClientFactory.CreateClient();

        foreach (var dec in decorations)
        {
            var url = $"https://www.linkedin.com/voyager/api/identity/dash/profiles" +
                      $"?q=memberIdentity&memberIdentity={Uri.EscapeDataString(vanity)}&decorationId={dec}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            // Must send cookies exactly as they exist in the browser session
            request.Headers.TryAddWithoutValidation("Cookie", $"li_at={liAt}; JSESSIONID=\"{jsession}\"");
            request.Headers.TryAddWithoutValidation("csrf-token", jsession);
            request.Headers.TryAddWithoutValidation("x-restli-protocol-version", "2.0.0");
            request.Headers.TryAddWithoutValidation("x-li-lang", "en_US");
            request.Headers.TryAddWithoutValidation("accept", "application/vnd.linkedin.normalized+json+2.1");
            request.Headers.TryAddWithoutValidation("user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");

            try
            {
                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                logger.LogInformation("Voyager [{Dec}] HTTP {Status} body[0..500]: {Body}",
                    dec[^2..], (int)response.StatusCode,
                    body[..Math.Min(500, body.Length)]);

                if (response.IsSuccessStatusCode && body.Length > 100)
                    return ParseVoyagerResponse(body);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Voyager call failed: {Msg}", ex.Message);
            }
        }

        return null;
    }

    private ProfileData ParseVoyagerResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Main profile is in elements[0]
        JsonElement profile = default;
        if (root.TryGetProperty("elements", out var elements) && elements.GetArrayLength() > 0)
            profile = elements[0];

        var data = new ProfileData { FetchedAt = DateTime.UtcNow };

        if (profile.ValueKind != JsonValueKind.Undefined)
        {
            var firstName = GetStr(profile, "firstName");
            var lastName = GetStr(profile, "lastName");
            data.Name = string.IsNullOrEmpty(firstName) ? string.Empty : $"{firstName} {lastName}".Trim();
            data.Headline = GetStr(profile, "headline") ?? string.Empty;
            data.Location = GetStr(profile, "geoLocationName") ?? GetStr(profile, "locationName") ?? string.Empty;
            data.About = GetStr(profile, "summary") ?? string.Empty;
        }

        // Everything else is in included[]
        if (!root.TryGetProperty("included", out var included)) return data;

        foreach (var item in included.EnumerateArray())
        {
            if (!item.TryGetProperty("entityUrn", out var urnEl)) continue;
            var urn = urnEl.GetString() ?? "";

            // Photo
            if (string.IsNullOrEmpty(data.PhotoUrl)
                && item.TryGetProperty("vectorImage", out var vec)
                && vec.TryGetProperty("artifacts", out var arts)
                && arts.GetArrayLength() > 0)
            {
                var rootUrl = vec.TryGetProperty("rootUrl", out var ru) ? ru.GetString() : "";
                var best = arts.EnumerateArray()
                    .OrderByDescending(a => a.TryGetProperty("width", out var w) ? w.GetInt32() : 0)
                    .FirstOrDefault();
                if (best.ValueKind != JsonValueKind.Undefined
                    && best.TryGetProperty("fileIdentifyingUrlPathSegment", out var seg))
                    data.PhotoUrl = (rootUrl ?? "") + seg.GetString();
            }

            // Experience
            if (urn.Contains("fsd_profileExperience") || urn.Contains("profilePosition"))
            {
                var title = GetStr(item, "title");
                if (string.IsNullOrEmpty(title)) continue;
                var company = string.Empty;
                if (item.TryGetProperty("company", out var co)) company = GetStr(co, "name") ?? string.Empty;
                if (string.IsNullOrEmpty(company)) company = GetStr(item, "companyName") ?? string.Empty;
                var (start, end, isCurrent) = ParseTimePeriod(item);
                data.Experiences.Add(new ExperienceData
                {
                    Title = title,
                    Company = company,
                    StartDate = start,
                    EndDate = end,
                    Description = GetStr(item, "description") ?? string.Empty,
                    IsCurrent = isCurrent
                });
            }

            // Education
            if (urn.Contains("fsd_profileEducation") || urn.Contains("profileEducation"))
            {
                var school = string.Empty;
                if (item.TryGetProperty("school", out var sc)) school = GetStr(sc, "name") ?? string.Empty;
                if (string.IsNullOrEmpty(school)) school = GetStr(item, "schoolName") ?? string.Empty;
                if (string.IsNullOrEmpty(school)) continue;
                var years = ParseEdYears(item);
                data.Educations.Add(new EducationData
                {
                    School = school,
                    Degree = GetStr(item, "degreeName") ?? string.Empty,
                    FieldOfStudy = GetStr(item, "fieldOfStudy") ?? string.Empty,
                    StartYear = years.start,
                    EndYear = years.end
                });
            }

            // Skill
            if (urn.Contains("fsd_skill") || urn.Contains("profileSkill"))
            {
                var name = GetStr(item, "name");
                if (!string.IsNullOrEmpty(name))
                    data.Skills.Add(new SkillData { Name = name });
            }
        }

        return data;
    }

    private static string? GetStr(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static (string start, string end, bool isCurrent) ParseTimePeriod(JsonElement item)
    {
        if (!item.TryGetProperty("timePeriod", out var tp)) return ("", "", false);
        var start = FormatDate(tp.TryGetProperty("startDate", out var sd) ? sd : default);
        var end = FormatDate(tp.TryGetProperty("endDate", out var ed) ? ed : default);
        return (start, end, isCurrent: end == "" && start != "");
    }

    private static string FormatDate(JsonElement d)
    {
        if (d.ValueKind == JsonValueKind.Undefined) return string.Empty;
        var y = d.TryGetProperty("year", out var yr) ? yr.GetInt32().ToString() : "";
        var m = d.TryGetProperty("month", out var mo) ? mo.GetInt32().ToString().PadLeft(2, '0') : "";
        return string.IsNullOrEmpty(m) ? y : $"{y}-{m}";
    }

    private static (string start, string end) ParseEdYears(JsonElement item)
    {
        var s = item.TryGetProperty("startMonthYear", out var sy) && sy.TryGetProperty("year", out var sy2)
            ? sy2.GetInt32().ToString() : "";
        var e = item.TryGetProperty("endMonthYear", out var ey) && ey.TryGetProperty("year", out var ey2)
            ? ey2.GetInt32().ToString() : "";
        return (s, e);
    }
}
