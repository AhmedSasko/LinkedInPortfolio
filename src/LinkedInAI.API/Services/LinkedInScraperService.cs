using System.Text.Json;
using System.Text.RegularExpressions;
using PuppeteerSharp;

namespace LinkedInAI.API.Services;

public class LinkedInScraperService(IConfiguration config, ILogger<LinkedInScraperService> logger) : ILinkedInScraperService
{
    private record BasicInfo(string? Name, string? Headline, string? Location, string? About, string? PhotoUrl);

    private static readonly string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    public async Task<ProfileData> ScrapeProfileAsync(string profileUrl, string liAtCookie, Action<string>? onStep = null)
    {
        var executablePath = config["Puppeteer:ExecutablePath"]
            ?? Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");

        if (string.IsNullOrEmpty(executablePath))
            throw new InvalidOperationException(
                "Browser executable path is not configured. " +
                "Set Puppeteer:ExecutablePath in appsettings or PUPPETEER_EXECUTABLE_PATH env var.");

        var launchOptions = new LaunchOptions
        {
            Headless = true,
            Args = [
                "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage",
                "--disable-gpu", "--no-first-run", "--no-zygote",
                "--disable-blink-features=AutomationControlled",
                "--window-size=1280,900"
            ],
            ExecutablePath = executablePath
        };

        onStep?.Invoke("launching_browser");
        await using var browser = await Puppeteer.LaunchAsync(launchOptions);

        // Open all pages upfront — same browser context shares the cookie
        var mainPage  = await browser.NewPageAsync();
        var expPage   = await browser.NewPageAsync();
        var eduPage   = await browser.NewPageAsync();
        var skillPage = await browser.NewPageAsync();
        var projPage  = await browser.NewPageAsync();

        var cookie = new CookieParam
        {
            Name = "li_at", Value = liAtCookie, Domain = ".linkedin.com",
            Path = "/", HttpOnly = true, Secure = true
        };

        // Inject anti-detection script before any navigation (must run on new document)
        const string stealthScript = @"
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
            Object.defineProperty(navigator, 'plugins', { get: () => [1,2,3,4,5] });
            Object.defineProperty(navigator, 'languages', { get: () => ['en-US','en'] });
            window.chrome = { runtime: {}, loadTimes: function(){}, csi: function(){}, app: {} };
        ";
        foreach (var p in new[] { mainPage, expPage, eduPage, skillPage, projPage })
            await p.EvaluateExpressionOnNewDocumentAsync(stealthScript);

        // Set UA + cookie on every page
        foreach (var p in new[] { mainPage, expPage, eduPage, skillPage, projPage })
        {
            await p.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 900 });
            await p.SetUserAgentAsync(UserAgent);
            await p.SetCookieAsync(cookie);
        }

        // ── Launch all navigations in parallel ───────────────────────────────
        onStep?.Invoke("loading_profile");

        static Task SafeGoto(IPage p, string url) =>
            p.GoToAsync(url, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
                Timeout = 30000
            }).ContinueWith(_ => { }, TaskContinuationOptions.None); // swallow exceptions

        // Ensure no trailing slash before appending detail paths
        var baseUrl = profileUrl.TrimEnd('/');
        await Task.WhenAll(
            SafeGoto(mainPage,  baseUrl),
            SafeGoto(expPage,   baseUrl + "/details/experience/"),
            SafeGoto(eduPage,   baseUrl + "/details/education/"),
            SafeGoto(skillPage, baseUrl + "/details/skills/"),
            SafeGoto(projPage,  baseUrl + "/details/projects/")
        );

        // Check auth on main page
        if (IsAuthWall(mainPage.Url))
            throw new InvalidOperationException(
                "LinkedIn session expired. Please log into LinkedIn, copy a fresh li_at cookie and try again.");

        logger.LogInformation("Main page URL: {Url}", mainPage.Url);
        logger.LogInformation("Detail page URLs: exp={E}, edu={Ed}, skills={S}, proj={P}",
            expPage.Url, eduPage.Url, skillPage.Url, projPage.Url);

        // Wait for JS hydration then progressively scroll to trigger IntersectionObserver lazy-loading
        await Task.Delay(3000);
        // Try to wait for profile sections to appear (h2 beyond "Activity")
        try { await mainPage.WaitForSelectorAsync("section", new WaitForSelectorOptions { Timeout = 5000 }); } catch { /* proceed */ }
        await mainPage.EvaluateFunctionAsync<object>(@"async () => {
            for (let y = 300; y <= 5000; y += 250) {
                window.scrollTo(0, y);
                await new Promise(r => setTimeout(r, 100));
            }
            window.scrollTo(0, 0);
        }");
        await Task.Delay(3000);

        // Log main page body sample to understand About/structure
        var mainBodySample = await mainPage.EvaluateFunctionAsync<string>(@"() => document.body.innerText.substring(0, 8000)");
        logger.LogInformation("Main page body (first 8000): {Body}", mainBodySample);

        // Wait for detail pages to JS-render (they are CSR-only, need extra time)
        // We wait up to 6s for span[aria-hidden] to appear, meaning content has loaded
        await Task.WhenAll(
            WaitForDetailContent(expPage),
            WaitForDetailContent(eduPage),
            WaitForDetailContent(skillPage),
            WaitForDetailContent(projPage)
        );

        // ── DOM diagnostics ──────────────────────────────────────────────────
        var domDiag = await mainPage.EvaluateFunctionAsync<string>(@"() => {
            const aboutEl = document.querySelector('#about');
            const h2h3 = [...document.querySelectorAll('h2,h3')].map(e => (e.textContent||'').trim().substring(0,60));
            const ariaSpans = [...document.querySelectorAll('span[aria-hidden=""true""]')]
                .map(s => (s.textContent||'').trim().length);
            const ogDesc = document.querySelector('meta[property=""og:description""]')?.getAttribute('content') || null;
            const jsonLd = [...document.querySelectorAll('script[type=""application/ld+json""]')]
                .map(s => s.textContent||'').join(' | ').substring(0, 500);
            return JSON.stringify({
                hasAbout: !!aboutEl,
                aboutTag: aboutEl?.tagName,
                h2h3,
                ariaSpanLengths: ariaSpans.filter(l => l > 40).sort((a,b)=>b-a).slice(0,10),
                ogDesc: ogDesc ? ogDesc.substring(0, 200) : null,
                jsonLd: jsonLd.substring(0, 200)
            });
        }");
        logger.LogInformation("DOM diagnostics: {Diag}", domDiag);

        // ── RSC profileCardsAboveActivity: fetch About section directly ─────────
        // LinkedIn serves a stripped page to headless Chrome (no About/Experience sections),
        // but the RSC component endpoint returns the full profile data when called with
        // a valid session cookie (li_at + JSESSIONID) and the vieweeProfileId.
        var profileVanity = baseUrl.TrimEnd('/').Split('/').Last();
        string? rscAbout = null;
        try
        {
            // Step 1: extract vieweeProfileId — present in the "Message" button href
            // as ?profileUrn=urn%3Ali%3Afsd_profile%3AACoA... even in the partial headless page.
            var vieweeProfileId = await mainPage.EvaluateFunctionAsync<string?>(@"() => {
                for (const link of document.querySelectorAll('a[href]')) {
                    const href = link.getAttribute('href') || '';
                    if (!href.includes('ACoA')) continue;
                    const m = href.match(/(ACoA[A-Za-z0-9+\/=_-]{10,60})/);
                    if (m) return m[1];
                }
                for (const el of document.querySelectorAll('[componentkey]')) {
                    const key = el.getAttribute('componentkey') || '';
                    const m = key.match(/ref(ACoA[A-Za-z0-9+\/=_-]{10,50})/);
                    if (m) return m[1];
                }
                return null;
            }");

            logger.LogInformation("RSC: vieweeProfileId={Id}", vieweeProfileId);

            if (!string.IsNullOrEmpty(vieweeProfileId))
            {
                // Step 2: POST to profileCardsAboveActivity RSC endpoint from within the page
                // context so cookies (li_at + JSESSIONID) are automatically included.
                var rscRaw = await mainPage.EvaluateFunctionAsync<string?>(@"async (profileId, vanity) => {
                    try {
                        const jsid = document.cookie.split(';').map(c=>c.trim()).find(c=>c.startsWith('JSESSIONID='));
                        const csrf = jsid ? jsid.split('=').slice(1).join('=') : '';
                        const payload = {
                            clientArguments: {
                                payload: {
                                    isSelfView: false,
                                    vanityName: vanity,
                                    replaceableSectionArgs: {
                                        vanityName: vanity,
                                        hideCardsForGoldenGate: false,
                                        shouldSetupReplaceableComponent: true,
                                        vieweeProfileId: profileId,
                                        isSelfView: false
                                    },
                                    profileComponentState: { profileId: vanity }
                                },
                                states: [],
                                requestMetadata: {},
                                screenId: 'com.linkedin.sdui.flagshipnav.profile.Profile'
                            }
                        };
                        const r = await fetch(
                            '/flagship-web/rsc-action/actions/component' +
                            '?componentId=com.linkedin.sdui.generated.profile.dsl.impl.profileCardsAboveActivity' +
                            '&sduiid=com.linkedin.sdui.generated.profile.dsl.impl.profileCardsAboveActivity' +
                            '&parentSpanId=AAAAAAAAAA%3D',
                            {
                                method: 'POST',
                                credentials: 'include',
                                headers: {
                                    'content-type': 'application/json',
                                    'csrf-token': csrf,
                                    'x-li-rsc-stream': 'true',
                                    'x-li-application-version': '0.2.5496',
                                    'x-li-anchor-page-key': 'd_flagship3_profile_view_base'
                                },
                                body: JSON.stringify(payload)
                            }
                        );
                        if (!r.ok) return null;
                        const t = await r.text();
                        return t.substring(0, 60000);
                    } catch(e) { return null; }
                }", vieweeProfileId, profileVanity);

                if (!string.IsNullOrEmpty(rscRaw))
                {
                    logger.LogInformation("RSC response length: {Len}", rscRaw.Length);
                    rscAbout = ParseBioFromRscResponse(rscRaw);
                    logger.LogInformation("RSC About: {About}", rscAbout?[..Math.Min(rscAbout?.Length ?? 0, 100)]);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("RSC about fetch failed: {Msg}", ex.Message);
        }

        // ── Extract basic info from main page ────────────────────────────────
        onStep?.Invoke("basic_info");
        var basic = await ExtractBasicInfo(mainPage);

        // ── Parse detail pages ───────────────────────────────────────────────
        onStep?.Invoke("experience");
        var experiences = await ExtractDetailItems<List<ExperienceData>>(expPage, ExperienceExtractorJs) ?? [];

        onStep?.Invoke("education");
        var educations = await ExtractDetailItems<List<EducationData>>(eduPage, EducationExtractorJs) ?? [];

        onStep?.Invoke("skills");
        var skills = await ExtractDetailItems<List<SkillData>>(skillPage, SkillsExtractorJs) ?? [];

        onStep?.Invoke("certifications");
        var projects = await ExtractDetailItems<List<ProjectData>>(projPage, ProjectsExtractorJs) ?? [];
        if (projects.Count == 0)
        {
            logger.LogInformation("Projects aria-span extractor returned 0 — trying body-text parser");
            projects = await ExtractDetailItems<List<ProjectData>>(projPage, ProjectsBodyTextExtractorJs) ?? [];
        }

        // ── Fallback: if detail pages failed (auth-walled or empty), use top-card ──
        if (experiences.Count == 0 && educations.Count == 0)
        {
            logger.LogWarning("Detail pages yielded nothing — falling back to top-card extraction");
            var (topExp, topEdu) = await ExtractTopCardEntriesAsync(mainPage);
            experiences = topExp;
            educations  = topEdu;
        }

        logger.LogInformation(
            "DOM scrape result: Name={Name}, Headline={H}, Location={L}, About={A}, Exp={E}, Edu={Ed}, Skills={S}, Proj={P}",
            basic?.Name, basic?.Headline, basic?.Location,
            basic?.About?[..Math.Min(basic.About.Length, 60)],
            experiences.Count, educations.Count, skills.Count, projects.Count);

        return new ProfileData
        {
            Name      = basic?.Name,
            Headline  = basic?.Headline,
            Location  = basic?.Location,
            About     = rscAbout ?? basic?.About,   // RSC fetch preferred; DOM strategies as fallback
            PhotoUrl  = basic?.PhotoUrl,
            Experiences    = experiences,
            Educations     = educations,
            Skills         = skills,
            Certifications = [],
            Projects       = projects
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsAuthWall(string url) =>
        url.Contains("/login") || url.Contains("/authwall") || url.Contains("/uas/");

    // Wait up to 6 s for detail page content to JS-render (span[aria-hidden] or bodyLen > 5000)
    private static async Task WaitForDetailContent(IPage page)
    {
        if (IsAuthWall(page.Url)) return;
        try
        {
            // Try waiting for the first real content span
            await page.WaitForSelectorAsync("span[aria-hidden='true']",
                new WaitForSelectorOptions { Timeout = 6000 });
        }
        catch
        {
            // If no aria-spans appear, at least wait until body grows beyond nav-only size
            try
            {
                await page.WaitForFunctionAsync(
                    "() => document.body.innerText.length > 3000",
                    new WaitForFunctionOptions { Timeout = 4000 });
            }
            catch { /* proceed with whatever loaded */ }
        }
    }

    private async Task<T?> ExtractDetailItems<T>(IPage page, string extractorJs) where T : class
    {
        try
        {
            if (IsAuthWall(page.Url))
            {
                logger.LogWarning("Detail page is auth-walled: {Url}", page.Url);
                return null;
            }
            // Log body to understand page structure
            var bodySample = await page.EvaluateFunctionAsync<string>(@"() => {
                const spans = [...document.querySelectorAll('span[aria-hidden=""true""]')].slice(0,20).map(s => s.textContent?.trim()).filter(Boolean);
                return JSON.stringify({ url: location.href, bodyLen: document.body.innerText.length, bodyText: document.body.innerText.substring(0,2000), spanSamples: spans, liCount: document.querySelectorAll('li').length });
            }");
            logger.LogInformation("Detail page debug ({Url}): {Info}", page.Url, bodySample?[..Math.Min(bodySample?.Length ?? 0, 1500)]);

            var result = await page.EvaluateFunctionAsync<T>(extractorJs);
            var count = (result as System.Collections.ICollection)?.Count ?? -1;
            logger.LogInformation("Detail page result ({Url}): count={Count}", page.Url, count);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Detail page extraction failed ({Url}): {Msg}", page.Url, ex.Message);
            return null;
        }
    }

    // ── JavaScript extractors for each detail page ────────────────────────────

    // Shared logic: extract aria-hidden spans from each list item, deduped
    private const string AriaSpanExtractorHelper = @"
        function extractItems(filterFn, mapFn) {
            const seen = new Set();
            const results = [];
            document.querySelectorAll('li, article').forEach(el => {
                const spans = [...el.querySelectorAll('span[aria-hidden=""true""]')]
                    .map(s => (s.textContent || '').trim())
                    .filter(t => t && t.length > 1 && t.length < 600);
                if (spans.length < 1) return;
                if (!filterFn(spans)) return;
                const key = spans[0] + '|' + (spans[1] || '');
                if (seen.has(key)) return;
                seen.add(key);
                const mapped = mapFn(spans);
                if (mapped) results.push(mapped);
            });
            return results;
        }
    ";

    private static readonly string ExperienceExtractorJs = @"() => {
        " + AriaSpanExtractorHelper + @"
        const NAV = new Set(['Home','Jobs','Messaging','LinkedIn','Notifications','Me','For Business','Experience','Interests','Skills','Education','Projects','Licenses','Languages']);
        return extractItems(
            spans => spans[0].length > 1 && !NAV.has(spans[0]) && !spans[0].match(/^\d+$/),
            spans => {
                const title = spans[0];
                // Company is typically 'Name · Type' or just 'Name'
                const companyRaw = spans[1] || null;
                const company = companyRaw ? companyRaw.split(' · ')[0].trim() : null;
                // Find the date span — contains a 4-digit year or 'Present' / Arabic equivalents
                const dateSpan = spans.find(t => t.match(/\d{4}/) && (t.includes('-') || t.includes('–') || t.includes('Present') || t.includes('\u062d\u0627\u0644\u064a\u064b\u0627') || t.includes('\u0627\u0644\u0622\u0646')));
                let startDate = null, endDate = null;
                let isCurrent = false;
                if (dateSpan) {
                    isCurrent = dateSpan.includes('Present') || dateSpan.includes('\u062d\u0627\u0644\u064a\u064b\u0627') || dateSpan.includes('\u0627\u0644\u0622\u0646') || dateSpan.includes('\u062d\u062a\u0649 \u0627\u0644\u0622\u0646');
                    const parts = dateSpan.split(/\s*[-–]\s*/);
                    startDate = parts[0]?.trim() || null;
                    if (!isCurrent && parts.length > 1) {
                        // strip duration suffix like '· 2 years'
                        endDate = parts[1]?.split(' · ')[0]?.trim() || null;
                    }
                }
                // Description: longer free-text span
                const desc = spans.find(t => t.length > 60 && !t.match(/^\d/) && t !== companyRaw && t !== title && !t.match(/\d{4}/));
                return { title, company, startDate, endDate, description: desc || null, isCurrent };
            }
        ).slice(0, 30);
    }";

    private static readonly string EducationExtractorJs = @"() => {
        " + AriaSpanExtractorHelper + @"
        const NAV = new Set(['Home','Jobs','Messaging','LinkedIn','Notifications','Me','For Business','Education','Experience','Skills','Projects']);
        return extractItems(
            spans => spans[0].length > 2 && !NAV.has(spans[0]) && !spans[0].match(/^\d+$/),
            spans => {
                const school = spans[0];
                // Second span is often 'Field · Degree' or 'Degree, Field'
                const degreeRaw = spans[1] || null;
                let degree = null, fieldOfStudy = null;
                if (degreeRaw) {
                    const parts = degreeRaw.split(' · ');
                    fieldOfStudy = parts[0]?.trim() || null;
                    degree = parts[1]?.trim() || parts[0]?.trim() || null;
                }
                // Year span: '2004 — 2008' or Arabic numerals
                const yearSpan = spans.find(t => t.match(/[\d\u0660-\u0669]{4}\s*[—–-]/));
                let startYear = null, endYear = null;
                if (yearSpan) {
                    const parts = yearSpan.split(/\s*[—–-]\s*/);
                    startYear = parts[0]?.trim() || null;
                    endYear = parts[1]?.trim() || null;
                }
                return { school, degree, fieldOfStudy, startYear, endYear };
            }
        ).slice(0, 20);
    }";

    private static readonly string SkillsExtractorJs = @"() => {
        " + AriaSpanExtractorHelper + @"
        const NAV = new Set(['Home','Jobs','Messaging','LinkedIn','Notifications','Me','For Business','Skills','Add a skill','Show all skills']);
        return extractItems(
            spans => spans[0].length > 1 && !NAV.has(spans[0]) && !spans[0].match(/^\d+$/) && spans[0].length < 80,
            spans => {
                const name = spans[0];
                // Endorsement count: span like '47 endorsements'
                const endorse = spans.find(t => t.match(/^\d+\s+(endorsement|people)/i));
                const endorsementCount = endorse ? (parseInt(endorse.match(/\d+/)?.[0] || '0') || 0) : 0;
                return { name, endorsementCount };
            }
        ).slice(0, 60);
    }";

    private static readonly string ProjectsExtractorJs = @"() => {
        " + AriaSpanExtractorHelper + @"
        const NAV = new Set(['Home','Jobs','Messaging','LinkedIn','Notifications','Me','For Business','Projects','Show project']);
        return extractItems(
            spans => spans[0].length > 2 && !NAV.has(spans[0]) && !spans[0].match(/^\d+$/),
            spans => {
                const title = spans[0];
                // Date span
                const dateSpan = spans.find(t => t.match(/[\d\u0660-\u0669]{4}/) || t.includes('Present') || t.includes('\u062d\u0627\u0644\u064a\u064b\u0627'));
                let startDate = null, endDate = null;
                if (dateSpan) {
                    const parts = dateSpan.split(/\s*[—–-]\s*/);
                    startDate = parts[0]?.trim() || null;
                    endDate = parts[1]?.trim() || null;
                }
                // Description: first long text
                const desc = spans.find(t => t.length > 30 && t !== title && !t.match(/^\d/) && !t.match(/\d{4}/));
                // URL: look for url in spans or data attributes
                return { title, description: desc || null, url: null, startDate, endDate };
            }
        ).slice(0, 20);
    }";

    // Projects body-text extractor — used when aria-span extractor finds nothing.
    // Handles both English and Arabic LinkedIn profiles.
    private static readonly string ProjectsBodyTextExtractorJs = @"() => {
        const clean = s => s.replace(/[\u200F\u200E\u200B\u200C\u200D\uFEFF]/g, '').trim();
        const body = document.body.innerText || '';
        const lines = body.split('\n').map(l => clean(l)).filter(l => l.length > 0);

        // Section headings in English + Arabic
        const SKIP = new Set(['Show project','عرض المشروع','Home','Jobs','Messaging',
            'Notifications','Me','For Business','LinkedIn','Add project',
            'Projects','المشروعات','Show all projects','مساهمون آخرون','Other contributors']);
        const STOP = new Set(['Experience','Education','Skills','Honors','Recommendations',
            'Certifications','Languages','Interests','About','حول',
            'Licenses & certifications','خبرة','التعليم','الخبرات','المهارات',
            'More profiles for you','المزيد من الملفات الشخصية']);
        const SKIP_LINE = new Set(['Connect','Message','Follow']);
        // Additional skip: lines starting with · (degree of separation indicator)

        const startIdx = lines.findIndex(l => l === 'Projects' || l === 'المشروعات');
        if (startIdx < 0) return [];

        const results = [];
        let title = null, startDate = null, endDate = null, desc = null;

        const commit = () => {
            if (title && title.length > 2 && !SKIP.has(title) && !STOP.has(title))
                results.push({ title, description: desc, url: null, startDate, endDate });
            title = null; startDate = null; endDate = null; desc = null;
        };

        for (let i = startIdx + 1; i < lines.length; i++) {
            const line = lines[i];
            if (STOP.has(line)) break;
            if (!line || line.length < 2) continue;
            // Skip navigation / meta lines
            if (SKIP.has(line) || SKIP_LINE.has(line)) continue;
            if (line.startsWith('Associated with') || line.startsWith('مرتبط بـ')) continue;
            if (line.startsWith('Show all')) continue;
            if (line.startsWith('·') || line.startsWith('•')) continue;
            // Skip contributor counts like +10 or Arabic +١٠
            if (/^\+[\d\u0660-\u0669]+$/.test(line)) continue;
            // Skip pure numbered list prefixes: 1- or ١-
            if (/^[\d\u0660-\u0669]+[\.\-\)]\s*$/.test(line)) continue;
            // Skip section-label lines (end with colon, e.g. My Role:)
            if (line.endsWith(':')) continue;

            // Date line: 4-digit Western or Arabic-Indic year + dash or Present/حاليًا
            const isDate = (/\d{4}/.test(line) || /[\u0660-\u0669]{4}/.test(line))
                && (/[–—\-]/.test(line) || /Present|حالياً|حاليًا/.test(line));
            if (isDate) {
                if (title) {
                    const parts = line.split(/\s*[–—\-]\s*/);
                    startDate = parts[0]?.replace(/[\u200F\u200E]/g, '').trim() || null;
                    const endPart = parts[1]?.split(' · ')[0]?.replace(/[\u200F\u200E]/g, '').trim() || null;
                    endDate = /Present|حالياً|حاليًا/.test(endPart || '') ? null : endPart;
                }
                continue;
            }

            // Long description line
            if (title && line.length > 40 && !desc) { desc = line; continue; }

            // Short continuation: numbered list item like 1- Do something...
            if (title && /^[\d\u0660-\u0669]+[\.\-\)]/.test(line)) continue;

            // Otherwise: start a new project
            commit();
            title = line;
        }
        commit();
        return results.slice(0, 20);
    }";

    // ── Basic info extraction from main profile page ──────────────────────────

    private static async Task<BasicInfo?> ExtractBasicInfo(IPage page)
    {
        try { await page.WaitForSelectorAsync("h1", new WaitForSelectorOptions { Timeout = 5000 }); }
        catch { /* proceed */ }

        return await page.EvaluateFunctionAsync<BasicInfo>(@"() => {
            // Name
            const h1 = document.querySelector('h1');
            let name = h1 ? (h1.textContent||'').trim() || null : null;
            if (!name) {
                const t = document.title;
                const sep = t.indexOf(' | ');
                if (sep > 0) name = t.substring(0, sep).trim() || null;
            }

            const cleanLine = l => l.replace(/[\u200F\u200E\u200B\u200C\u200D\uFEFF]/g, '').trim();
            const lines = (document.body.innerText || '').split('\n').map(cleanLine).filter(Boolean);

            // Headline: first meaningful sibling of h1
            let headline = null;
            if (h1) {
                let sib = h1.nextElementSibling;
                for (let i = 0; i < 5 && sib && !headline; i++) {
                    const spans = [...sib.querySelectorAll('span[aria-hidden=""true""]')]
                        .map(s => (s.textContent||'').trim()).filter(Boolean);
                    if (spans.length > 0) { headline = spans[0]; break; }
                    const txt = (sib.textContent||'').trim();
                    if (txt && txt.length > 3 && txt.length < 300 &&
                        !txt.includes('Connect') && !txt.includes('Follow') && !txt.includes('Message'))
                        { headline = txt; break; }
                    sib = sib.nextElementSibling;
                }
            }
            if (!headline && name) {
                const nameIdx = lines.indexOf(name);
                if (nameIdx >= 0) {
                    for (let i = nameIdx + 1; i < nameIdx + 5 && i < lines.length; i++) {
                        const l = lines[i];
                        if (l && l.length > 3 && l.length < 200 &&
                            !l.match(/^·/) && !l.includes('Connect') && !l.includes('Message'))
                            { headline = l; break; }
                    }
                }
            }

            // Location
            let location = null;
            const contactIdx = lines.findIndex(l => l === 'Contact info' || l.startsWith('Contact info'));
            if (contactIdx > 0) {
                for (let i = Math.max(0, contactIdx - 5); i < contactIdx + 3; i++) {
                    const l = lines[i];
                    if (l && l !== name && l !== headline && l.length > 3 && l.length < 100 &&
                        (l.includes(',') || l.includes('Saudi') || l.includes('Arabia') ||
                         l.match(/^[A-Z][a-z]/) || l.match(/[\u0600-\u06FF]/))) {
                        if (!l.includes('·') && !l.match(/^\d/) && !l.includes('Connect') && !l.includes('Follow')) {
                            location = l; break;
                        }
                    }
                }
            }
            if (!location && h1) {
                const card = h1.closest('section') ?? h1.parentElement?.parentElement;
                if (card) {
                    const spans = [...card.querySelectorAll('span[aria-hidden=""true""], button span')]
                        .map(s => (s.textContent||'').trim())
                        .filter(t => t && t !== name && t !== headline && t.length > 3 && t.length < 100);
                    location = spans.find(t =>
                        t.includes(',') || t.includes('Saudi') || t.includes('Arabia') ||
                        (t.match(/^[A-Z]/) && t.length < 60)) ?? null;
                }
            }

            // About — handles both English ('About') and Arabic ('نبذة عنا', 'نبذة عني', 'نبذة', 'حول') headings
            let about = null;
            const aboutHeadings = new Set(['About', '\u0646\u0628\u0630\u0629 \u0639\u0646\u0627', '\u0646\u0628\u0630\u0629 \u0639\u0646\u064a', '\u0646\u0628\u0630\u0629', '\u062d\u0648\u0644']);
            const socialSignals = ['Like','Comment','Repost','Share','Congratulations',
                '\u0645\u0628\u0631\u0648\u0648\u0648\u0643','\u064a\u0639\u062c\u0628\u0646\u064a','\u062a\u0639\u0644\u064a\u0642','\u0645\u0634\u0627\u0631\u0643\u0629','\u0625\u0639\u062c\u0627\u0628'];

            // Helper: extract bio text from a section element (tries multiple selectors)
            const extractFromSec = sec => {
                if (!sec) return null;
                // aria-hidden spans
                const spans = [...sec.querySelectorAll('span[aria-hidden=""true""]')]
                    .map(s => (s.textContent||'').trim()).filter(t => t && !aboutHeadings.has(t) && t.length > 80);
                if (spans.length > 0) return spans[0].substring(0, 3000);
                // expandable text box
                const box = sec.querySelector('[data-testid=""expandable-text-box""]');
                if (box) { const t = (box.textContent||'').trim(); if (t.length > 80) return t.substring(0, 3000); }
                // section innerText minus heading (catches non-aria-hidden bios)
                const raw = (sec.innerText||'').split('\n').map(cleanLine).filter(l => l.length > 2 && !aboutHeadings.has(l) && l !== 'see more' && l !== 'Show less').join(' ');
                if (raw.length > 80) return raw.substring(0, 3000);
                return null;
            };

            // 1. #about anchor → parent section
            const aboutEl = document.querySelector('#about');
            if (aboutEl) {
                const sec = aboutEl.closest('section') || aboutEl.parentElement?.parentElement;
                about = extractFromSec(sec);
            }

            // 2. h2/h3 matching any About heading variant → closest section
            if (!about) {
                const h = [...document.querySelectorAll('h2, h3')]
                    .find(el => aboutHeadings.has(cleanLine(el.textContent||'')));
                if (h) {
                    const sec = h.closest('section') || h.parentElement?.parentElement;
                    about = extractFromSec(sec);
                }
            }

            // 3. Broad page scan — all aria-hidden spans > 150 chars, no social signals
            //    (catches bio even when section heading is unrecognized)
            if (!about) {
                const candidates = [...document.querySelectorAll('span[aria-hidden=""true""]')]
                    .map(s => (s.textContent||'').trim())
                    .filter(t => t.length > 150 && !socialSignals.some(sig => t.includes(sig)));
                if (candidates.length > 0) about = candidates[0].substring(0, 3000);
            }

            // 4. Body-text fallback — find About or Arabic-About heading in rendered page text,
            //    collect until next section, require 200+ chars, reject social-media signals.
            if (!about) {
                const stopHeadings = new Set([
                    'Activity', '\u0646\u0634\u0627\u0637', '\u0627\u0644\u0646\u0634\u0627\u0637',
                    'Experience', '\u0627\u0644\u062e\u0628\u0631\u0627\u062a', '\u062e\u0628\u0631\u0629',
                    'Education', '\u0627\u0644\u062a\u0639\u0644\u064a\u0645',
                    'Skills', '\u0627\u0644\u0645\u0647\u0627\u0631\u0627\u062a',
                    'Recommendations', 'Certifications', 'Projects', '\u0627\u0644\u0645\u0634\u0631\u0648\u0639\u0627\u062a',
                    'Licenses', 'Interests', 'Languages'
                ]);
                const footerKeywords = new Set(['Accessibility','Talent Solutions','Community Guidelines','Careers']);
                const firstActivityIdx = lines.findIndex(l => l === 'Activity' || l === '\u0646\u0634\u0627\u0637' || l === '\u0627\u0644\u0646\u0634\u0627\u0637');
                for (let ai = 0; ai < lines.length; ai++) {
                    if (!aboutHeadings.has(lines[ai])) continue;
                    if (footerKeywords.has(lines[ai + 1] || '')) continue;
                    if (firstActivityIdx >= 0 && ai > firstActivityIdx) continue;
                    let endIdx = lines.length;
                    for (let i = ai + 1; i < lines.length; i++) {
                        if (stopHeadings.has(lines[i])) { endIdx = i; break; }
                    }
                    const chunk = lines.slice(ai + 1, endIdx)
                        .filter(l => l.length > 5 && !l.match(/^… /) && l !== '… more').join(' ');
                    if (chunk.length < 200) continue;
                    if (socialSignals.some(sig => chunk.includes(sig))) continue;
                    about = chunk.substring(0, 3000); break;
                }
            }

            const photoEl = document.querySelector('img[data-anonymize=""profile-photo""]')
                         ?? document.querySelector('img[alt*=""profile photo"" i]')
                         ?? document.querySelector('main img[src*=""licdn""]');

            return { name, headline, location, about, photoUrl: photoEl?.src ?? null };
        }");
    }

    // ── Parse bio from LinkedIn RSC wire-format response ─────────────────────
    // The RSC response for profileCardsAboveActivity embeds bio text as:
    //   "children":["BIO TEXT"]
    // (a single-element JSON array whose only element is the bio string)
    private static string? ParseBioFromRscResponse(string rscText)
    {
        string[] socialSignals = ["Like", "Comment", "Repost", "Congratulations",
            "مبروووك", "يعجبني", "تعليق", "مشاركة", "Endorsement", "endorsed"];

        // Match: "children":["...long string..."]
        const string pattern = @"""children""\s*:\s*\[""((?:[^""\\]|\\.){100,3000})""\]";
        foreach (Match m in Regex.Matches(rscText, pattern))
        {
            var candidate = m.Groups[1].Value
                .Replace(@"\n", " ")
                .Replace(@"\r", "")
                .Replace(@"\""", "\"")
                .Trim();

            if (candidate.Length < 100) continue;
            if (socialSignals.Any(s => candidate.Contains(s))) continue;
            return candidate[..Math.Min(candidate.Length, 3000)];
        }
        return null;
    }

    // ── Top-card fallback for 2nd-connection condensed view ───────────────────

    private static async Task<(List<ExperienceData> exp, List<EducationData> edu)> ExtractTopCardEntriesAsync(IPage page)
    {
        var result = await page.EvaluateFunctionAsync<string>(@"() => {
            const lines = (document.body.innerText || '').split('\n').map(l => l.trim()).filter(Boolean);
            const contactIdx = lines.findIndex(l => l === 'Contact info' || l.startsWith('Contact info'));
            const connIdx = lines.findIndex(l => l.includes('connection'));
            let company = null, school = null;
            if (contactIdx > 0 && connIdx > contactIdx) {
                const topCardItems = lines.slice(contactIdx + 1, connIdx)
                    .filter(l => l.length > 2 && !l.match(/^\d/) && !l.includes('·') && !l.includes(','));
                for (const item of topCardItems) {
                    const lower = item.toLowerCase();
                    if (!school && (lower.includes('college') || lower.includes('university') ||
                        lower.includes('institute') || lower.includes('school') || lower.includes('faculty')))
                        school = item;
                    else if (!company && item.length > 3)
                        company = item;
                }
                if (!company && topCardItems.length > 0) company = topCardItems[0];
                if (!school && topCardItems.length > 1) school = topCardItems[1];
            }
            return JSON.stringify({ company, school });
        }");

        var exp = new List<ExperienceData>();
        var edu = new List<EducationData>();
        if (result == null) return (exp, edu);

        try
        {
            using var doc = JsonDocument.Parse(result);
            var company = doc.RootElement.TryGetProperty("company", out var c) ? c.GetString() : null;
            var school  = doc.RootElement.TryGetProperty("school",  out var s) ? s.GetString() : null;
            if (!string.IsNullOrEmpty(company))
                exp.Add(new ExperienceData(null, company, null, null, null, true));
            if (!string.IsNullOrEmpty(school))
                edu.Add(new EducationData(school, null, null, null, null));
        }
        catch { /* ignore */ }

        return (exp, edu);
    }
}
