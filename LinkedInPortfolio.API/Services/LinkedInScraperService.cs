using System.Text.Json;
using PuppeteerSharp;

namespace LinkedInPortfolio.API.Services;

public class LinkedInScraperService(
    IHttpClientFactory httpClientFactory,
    ILogger<LinkedInScraperService> logger,
    IHostEnvironment env) : ILinkedInScraperService
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ProfileData> ScrapeProfileAsync(string profileUrl)
    {
        if (string.IsNullOrWhiteSpace(profileUrl))
            throw new ArgumentException("Profile URL cannot be empty.", nameof(profileUrl));

        logger.LogInformation("Launching headless browser...");
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            DefaultViewport = null,
            Args = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu"]
        });

        await using var page = await browser.NewPageAsync();
        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");

        logger.LogInformation("Navigating to profile URL: {Url}", profileUrl);
        await page.GoToAsync(profileUrl, new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded], Timeout = 60000 });

        var landedUrl = page.Url;
        if (landedUrl.Contains("/login") || landedUrl.Contains("/authwall") || landedUrl.Contains("/checkpoint"))
            throw new InvalidOperationException(
                "Your LinkedIn profile is set to private. Go to LinkedIn → Settings → Visibility → Profile viewing options → set to Public, then try again.");

        // Wait for the name heading to appear before proceeding
        try { await page.WaitForSelectorAsync("h1", new WaitForSelectorOptions { Timeout = 15000 }); }
        catch { logger.LogWarning("h1 not found within 15s — continuing anyway"); }
        await Delay(2000, 3000);

        await ScrollPageFully(page);
        await ExpandSections(page);

        if (env.IsDevelopment())
        {
            var mainHtml = await page.EvaluateExpressionAsync<string>("document.body.innerHTML");
            var dumpPath = Path.Combine(Path.GetTempPath(), "linkedin_main.html");
            await File.WriteAllTextAsync(dumpPath, mainHtml ?? string.Empty);
            logger.LogInformation("Main document HTML saved to {Path} ({Size} bytes)", dumpPath, mainHtml?.Length ?? 0);
            var screenshotBytes = await page.ScreenshotDataAsync(new ScreenshotOptions { FullPage = true });
            await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "linkedin_profile.png"), screenshotBytes);
        }

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
                // ── Name ────────────────────────────────────────────────────────────
                // LinkedIn always has exactly one h1 on profile pages
                const nameEl = document.querySelector('h1');
                const name = nameEl?.innerText?.trim() ?? '';

                // ── Headline ─────────────────────────────────────────────────────────
                // Walk siblings/ancestors of h1 to find the next meaningful text block
                let headline = '';
                if (nameEl) {
                    const tryEl = (el) => {
                        if (!el) return '';
                        const t = el.innerText?.trim() ?? '';
                        // Reject same as name, empty, buttons, or very long strings
                        if (!t || t === name || t.length > 250 || el.tagName === 'BUTTON') return '';
                        if (el.querySelector('button, input, svg')) return '';
                        return t.split('\n')[0].trim();
                    };
                    // 1. Direct next sibling of h1
                    headline = tryEl(nameEl.nextElementSibling);
                    // 2. Parent's next sibling
                    if (!headline) headline = tryEl(nameEl.parentElement?.nextElementSibling);
                    // 3. Grandparent's next sibling
                    if (!headline) headline = tryEl(nameEl.parentElement?.parentElement?.nextElementSibling);
                    // 4. Any div/span with class containing 'break-words' near the top
                    if (!headline) {
                        const bw = Array.from(document.querySelectorAll('[class*=""break-words""]'))
                            .find(e => { const t = e.innerText?.trim(); return t && t !== name && t.length > 2 && t.length < 200; });
                        if (bw) headline = bw.innerText.trim().split('\n')[0];
                    }
                }

                // ── Location ─────────────────────────────────────────────────────────
                const locationPatterns = ['Region', 'Area', 'City', 'Province', 'المنطقة', 'منطقة', 'مدينة', 'Saudi Arabia', 'الرياض'];
                const allLeafTexts = Array.from(document.querySelectorAll('span, button'))
                    .filter(e => e.children.length === 0)
                    .map(e => e.innerText?.trim())
                    .filter(t => t && t.length > 2 && t.length < 80);
                const location = allLeafTexts.find(t =>
                    locationPatterns.some(p => t.includes(p)) ||
                    (t.includes(',') && t.length < 60 && !/^\d/.test(t) && !/[.@#]/.test(t))
                ) ?? '';

                // ── About ─────────────────────────────────────────────────────────────
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

                // ── Photo ─────────────────────────────────────────────────────────────
                const photoSrc = (name ? document.querySelector('img[alt=""' + name + '""]')?.src : null)
                    ?? document.querySelector('img.pv-top-card-profile-picture__image--show')?.src
                    ?? document.querySelector('img[class*=""profile-picture""]')?.src
                    ?? '';

                // ── Section extractor ─────────────────────────────────────────────────
                const findSectionItems = (keywords) => {
                    // Try section/div with id matching keyword
                    for (const kw of keywords) {
                        const byId = document.getElementById(kw);
                        if (byId) {
                            const items = byId.querySelectorAll('li');
                            if (items.length > 0) return extractListItems(items);
                        }
                    }
                    // Find heading with exact keyword text, walk up to collect <li>s
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
                        // Prefer aria-hidden spans (LinkedIn uses these for visible text)
                        const ariaHidden = Array.from(item.querySelectorAll('span[aria-hidden=""true""]'))
                            .map(e => e.innerText?.trim()).filter(t => t && t.length > 0);
                        const allTexts = ariaHidden.length > 0 ? ariaHidden
                            : [...new Set(Array.from(item.querySelectorAll('span, p, h3, h4, a'))
                                .map(e => e.innerText?.trim()).filter(t => t && t.length > 0))];
                        return { texts: [...new Set(allTexts)], link: item.querySelector('a[href]')?.href ?? '' };
                    }).filter(i => i.texts.length > 0);

                // ── Extract each section ──────────────────────────────────────────────
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

        var d = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        var photoBase64 = await DownloadPhoto(page, d.GetProperty("photoSrc").GetString() ?? string.Empty);

        return new ProfileData
        {
            FetchedAt = DateTime.UtcNow,
            Name = d.GetProperty("name").GetString() ?? string.Empty,
            Headline = d.GetProperty("headline").GetString() ?? string.Empty,
            Location = d.GetProperty("location").GetString() ?? string.Empty,
            About = d.GetProperty("about").GetString() ?? string.Empty,
            PhotoBase64 = photoBase64,
            Experiences = System.Text.Json.JsonSerializer.Deserialize<List<ExperienceData>>(d.GetProperty("experiences").GetRawText(), _jsonOpts) ?? [],
            Educations = System.Text.Json.JsonSerializer.Deserialize<List<EducationData>>(d.GetProperty("educations").GetRawText(), _jsonOpts) ?? [],
            Skills = System.Text.Json.JsonSerializer.Deserialize<List<SkillData>>(d.GetProperty("skills").GetRawText(), _jsonOpts) ?? [],
            Projects = System.Text.Json.JsonSerializer.Deserialize<List<ProjectData>>(d.GetProperty("projects").GetRawText(), _jsonOpts) ?? [],
            Certifications = System.Text.Json.JsonSerializer.Deserialize<List<CertificationData>>(d.GetProperty("certifications").GetRawText(), _jsonOpts) ?? []
        };
    }

    private async Task<string> DownloadPhoto(IPage page, string src)
    {
        try
        {
            if (string.IsNullOrEmpty(src) || src.StartsWith("data:")) return src ?? string.Empty;
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, src);
            request.Headers.Add("Referer", "https://www.linkedin.com/");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await client.SendAsync(request, cts.Token);
            var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token);
            return Convert.ToBase64String(bytes);
        }
        catch { return string.Empty; }
    }

    private static Task Delay(int minMs = 800, int maxMs = 1800)
        => Task.Delay(Random.Shared.Next(minMs, maxMs));
}
