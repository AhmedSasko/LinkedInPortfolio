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

        // Use a SINGLE browser tab that navigates sequentially.
        // Multiple simultaneous tabs loading LinkedIn detail pages is a clear bot
        // pattern — LinkedIn detects it and strips content (bodyLen ~1807 with no entries).
        // Sequential single-tab navigation mimics real user behaviour and avoids detection.
        var page = await browser.NewPageAsync();

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
        await page.EvaluateExpressionOnNewDocumentAsync(stealthScript);
        await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 900 });
        await page.SetUserAgentAsync(UserAgent);
        await page.SetCookieAsync(cookie);

        // ── Navigation helpers ────────────────────────────────────────────────
        // Main page: DOMContentLoaded is enough — we scroll manually after.
        static Task SafeGoto(IPage p, string url) =>
            p.GoToAsync(url, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
                Timeout = 30000
            }).ContinueWith(_ => { }, TaskContinuationOptions.None); // swallow exceptions

        // Detail pages: wait for Networkidle2 so LinkedIn's SDUI JS fully renders content
        // before the extractor runs. This is the key change vs. the parallel approach.
        static Task DetailGoto(IPage p, string url) =>
            p.GoToAsync(url, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle2],
                Timeout = 35000
            }).ContinueWith(_ => { }, TaskContinuationOptions.None);

        // Ensure no trailing slash before appending detail paths
        var baseUrl = profileUrl.TrimEnd('/');

        // ── Step 1: Load main page ────────────────────────────────────────────
        onStep?.Invoke("loading_profile");
        await SafeGoto(page, baseUrl);

        if (IsAuthWall(page.Url))
            throw new InvalidOperationException(
                "LinkedIn session expired. Please log into LinkedIn, copy a fresh li_at cookie and try again.");

        logger.LogInformation("Main page URL: {Url}", page.Url);

        // Wait for JS hydration then progressively scroll to trigger IntersectionObserver lazy-loading
        await Task.Delay(3000);
        try { await page.WaitForSelectorAsync("section", new WaitForSelectorOptions { Timeout = 5000 }); } catch { /* proceed */ }
        await page.EvaluateFunctionAsync<object>(@"async () => {
            for (let y = 300; y <= 5000; y += 250) {
                window.scrollTo(0, y);
                await new Promise(r => setTimeout(r, 100));
            }
            window.scrollTo(0, 0);
        }");
        await Task.Delay(3000);

        // Log main page body sample to understand About/structure
        var mainBodySample = await page.EvaluateFunctionAsync<string>(@"() => document.body.innerText.substring(0, 8000)");
        logger.LogInformation("Main page body (first 8000): {Body}", mainBodySample);

        // ── DOM diagnostics ──────────────────────────────────────────────────
        var domDiag = await page.EvaluateFunctionAsync<string>(@"() => {
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
            var vieweeProfileId = await page.EvaluateFunctionAsync<string?>(@"() => {
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
                var rscRaw = await page.EvaluateFunctionAsync<string?>(@"async (profileId, vanity) => {
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

        // ── Extract basic info from main page (before navigating away) ─────────
        onStep?.Invoke("basic_info");
        var basic = await ExtractBasicInfo(page);

        // ── Navigate to detail pages sequentially ─────────────────────────────
        // Single-tab sequential navigation mimics natural user behaviour.
        // Each page uses NetworkIdle2 so SDUI JS fully renders the section content.
        onStep?.Invoke("experience");
        await DetailGoto(page, baseUrl + "/details/experience/");
        await WaitForDetailContent(page);
        var experiences = await ExtractDetailItems<List<ExperienceData>>(page, ExperienceExtractorJs) ?? [];

        onStep?.Invoke("education");
        await DetailGoto(page, baseUrl + "/details/education/");
        await WaitForDetailContent(page);
        var educations = await ExtractDetailItems<List<EducationData>>(page, EducationExtractorJs) ?? [];

        onStep?.Invoke("skills");
        await DetailGoto(page, baseUrl + "/details/skills/");
        await WaitForDetailContent(page);
        var skills = await ExtractDetailItems<List<SkillData>>(page, SkillsExtractorJs) ?? [];

        onStep?.Invoke("certifications");
        await DetailGoto(page, baseUrl + "/details/projects/");
        await WaitForDetailContent(page);
        var projects = await ExtractDetailItems<List<ProjectData>>(page, ProjectsExtractorJs) ?? [];
        if (projects.Count == 0)
        {
            logger.LogInformation("Projects aria-span extractor returned 0 — trying body-text parser");
            projects = await ExtractDetailItems<List<ProjectData>>(page, ProjectsBodyTextExtractorJs) ?? [];
        }

        onStep?.Invoke("languages");
        await DetailGoto(page, baseUrl + "/details/languages/");
        await WaitForDetailContent(page);
        var languages = await ExtractDetailItems<List<LanguageData>>(page, LanguagesExtractorJs) ?? [];

        // ── Fallback: if detail pages failed (auth-walled or empty), use top-card ──
        if (experiences.Count == 0 && educations.Count == 0)
        {
            logger.LogWarning("Detail pages yielded nothing — falling back to top-card extraction");
            await SafeGoto(page, baseUrl);
            await Task.Delay(2000);
            var (topExp, topEdu) = await ExtractTopCardEntriesAsync(page);
            experiences = topExp;
            educations  = topEdu;
        }

        logger.LogInformation(
            "DOM scrape result: Name={Name}, Headline={H}, Location={L}, About={A}, Exp={E}, Edu={Ed}, Skills={S}, Proj={P}, Lang={L2}",
            basic?.Name, basic?.Headline, basic?.Location,
            basic?.About?[..Math.Min(basic.About.Length, 60)],
            experiences.Count, educations.Count, skills.Count, projects.Count, languages.Count);

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
            Projects       = projects,
            Languages      = languages
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsAuthWall(string url) =>
        url.Contains("/login") || url.Contains("/authwall") || url.Contains("/uas/");

    // Wait up to 8 s for detail page content to JS-render.
    // The new LinkedIn SDUI does NOT use span[aria-hidden="true"] on detail pages;
    // text is rendered directly. Wait for body to grow beyond the nav-only ~1600 bytes.
    private static async Task WaitForDetailContent(IPage page)
    {
        if (IsAuthWall(page.Url)) return;
        try
        {
            await page.WaitForFunctionAsync(
                "() => document.body.innerText.length > 1600",
                new WaitForFunctionOptions { Timeout = 8000 });
        }
        catch { /* proceed with whatever loaded */ }
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

    // Body-text experience extractor for the new LinkedIn SDUI (no aria-hidden spans).
    // Handles Arabic-Indic numerals, Arabic month names, and mixed-language profiles.
    private static readonly string ExperienceExtractorJs = @"() => {
        const clean = s => s.replace(/[\u200F\u200E\u200B\u200C\u200D\uFEFF]/g, '').trim();
        // Arabic-Indic digits (٠١٢٣٤٥٦٧٨٩) → Western
        const normDigits = s => s ? s.replace(/[\u0660-\u0669]/g, d => String.fromCharCode(d.charCodeAt(0) - 0x0660 + 48)) : s;

        const PRESENT = ['Present', '\u062d\u062a\u0649 \u0627\u0644\u0622\u0646', '\u0627\u0644\u0622\u0646', '\u062d\u0627\u0644\u064a\u064b\u0627', '\u062d\u0627\u0644\u064a\u0627', '\u062d\u0627\u0644\u064a\u064b\u0627'];
        const STOP = new Set([
            '\u0627\u0644\u062a\u0639\u0644\u064a\u0645', 'Education',
            '\u0627\u0644\u0645\u0647\u0627\u0631\u0627\u062a', 'Skills',
            '\u0627\u0644\u0645\u0634\u0631\u0648\u0639\u0627\u062a', 'Projects',
            '\u0627\u0644\u062a\u0648\u0635\u064a\u0627\u062a', 'Recommendations',
            '\u0627\u0644\u0634\u0647\u0627\u062f\u0627\u062a', 'Certifications',
            '\u0645\u0632\u064a\u062f \u0645\u0646 \u0627\u0644\u0645\u0644\u0641\u0627\u062a \u0627\u0644\u0634\u062e\u0635\u064a\u0629 \u0645\u0646 \u0623\u062c\u0644\u0643',
            'More profiles for you',
            '\u0646\u0628\u0630\u0629 \u0639\u0646\u0627', 'About',
            '\u0625\u0645\u0643\u0627\u0646\u064a\u0629 \u0627\u0644\u0648\u0635\u0648\u0644', 'Accessibility',
            '\u062d\u0644\u0648\u0644 \u0627\u0644\u0645\u0648\u0627\u0647\u0628', 'Talent Solutions',
            '\u0625\u0631\u0634\u0627\u062f\u0627\u062a \u0627\u0644\u0645\u062c\u062a\u0645\u0639', 'Community Guidelines'
        ]);

        const body = document.body.innerText || '';
        const lines = body.split('\n').map(clean).filter(l => l.length > 0);

        const startIdx = lines.findIndex(l => l === '\u0627\u0644\u062e\u0628\u0631\u0629' || l === 'Experience');
        if (startIdx < 0) return [];

        const hasYear = l => /\d{4}|[\u0660-\u0669]{4}/.test(l);
        const isDateLine = l => {
            if (!hasYear(l)) return false;
            const base = l.split(' \u00b7 ')[0];
            return /[-\u2013]/.test(base) || PRESENT.some(p => base.includes(p));
        };
        const isDurationOnly = l => /^[\u0660-\u0669\d]/.test(l) && /\u0633\u0646\u0629|\u0634\u0647\u0631|year|month/i.test(l) && !isDateLine(l.split('\u00b7')[0].trim());
        const isSkillLine = l => l.includes('\u0628\u0627\u0644\u0625\u0636\u0627\u0641\u0629 \u0625\u0644\u0649') && l.includes('\u0645\u0647\u0627\u0631\u0629');
        const isLocationLine = l => {
            if (l.length > 80 || isDateLine(l)) return false;
            if (/[\u0600-\u06FF]/.test(l) && l.length < 50) return true;
            if (/^(Saudi Arabia|KSA|UAE|Egypt|Jordan|Kuwait|Bahrain|Qatar|Oman|Syria|Lebanon|Iraq|Yemen|Morocco|Algeria|Tunisia|United Arab Emirates)$/i.test(l)) return true;
            return false;
        };
        const isJobTitle = l => {
            if (l.length > 100) return false;
            if (!/^[A-Z\u0600-\u06FF]/.test(l)) return false;
            if (/\b(am|is|are|was|were|have|has|had)\b/.test(l)) return false;
            if (/^(Develop|Build|Create|Work|Manage|Lead|Design|Implement|Maintain|Support|Provide|Deliver|Responsible|We are|We were)/i.test(l)) return false;
            if (/\b(Engineer|Developer|Manager|Director|Lead|Leader|Head|Officer|Specialist|Analyst|Designer|Architect|Consultant|Senior|Junior|Staff|Principal|VP|CTO|CEO|CFO|COO|Instructor|Programmer|Administrator|Coordinator|Supervisor|President|Associate|Technician|Trainer)\b/i.test(l)) return true;
            if (l.length < 60 && l.split(' ').length < 7) return true;
            return false;
        };

        const results = [];
        let title = null, company = null, startDate = null, endDate = null, isCurrent = false, desc = null;
        let seenDate = false;

        const commit = () => {
            if (title && title.length > 1) {
                results.push({
                    title: title.trim(),
                    company: company ? company.trim() : null,
                    startDate: normDigits(startDate),
                    endDate: normDigits(endDate),
                    description: desc ? desc.trim() : null,
                    isCurrent
                });
            }
            title = null; company = null; startDate = null; endDate = null;
            isCurrent = false; desc = null; seenDate = false;
        };

        for (let i = startIdx + 1; i < lines.length; i++) {
            const line = lines[i];
            if (!line) continue;
            if (STOP.has(line)) break;
            if (line.startsWith('\u00b7') || line.startsWith('\u2022')) continue;
            if (/^[\u0660-\u0669\d]+\s*(\u0625\u0634\u0639\u0627\u0631|notification|connection)/i.test(line)) continue;
            if (isSkillLine(line)) continue;
            if (isDurationOnly(line)) continue;
            if (/^(\u062f\u0648\u0627\u0645 \u0643\u0627\u0645\u0644|\u062f\u0648\u0627\u0645 \u062c\u0632\u0626\u064a|\u0639\u0645\u0644 \u062d\u0631|Full-time|Part-time|Contract|Internship|\u062a\u062f\u0631\u064a\u0628)$/.test(line)) continue;
            if (/^(\u062f\u0648\u0627\u0645 \u0645\u0646 \u0645\u0642\u0631|\u0639\u0646 \u0628\u064f\u0639\u062f|\u0647\u062c\u064a\u0646|On-site|Remote|Hybrid)$/.test(line)) continue;
            if (line.startsWith('\u062a\u062c\u0631\u0628\u0629 Premium')) continue;

            // Date line
            if (isDateLine(line)) {
                seenDate = true;
                const base = line.split(' \u00b7 ')[0].trim();
                const parts = base.split(/\s*[-\u2013]\s*/);
                startDate = parts[0]?.trim() || null;
                const ep = parts[1]?.trim() || null;
                isCurrent = PRESENT.some(p => (ep || '').includes(p));
                endDate = isCurrent ? null : ep;
                continue;
            }

            // Company line with ' · ' separator
            if (line.includes(' \u00b7 ') && !hasYear(line)) {
                if (seenDate) { continue; } // location · work-mode after date
                if (!company && title) { company = line.split(' \u00b7 ')[0].trim(); }
                continue;
            }

            // After date: location → skip; job title → new entry; description → capture once
            if (seenDate) {
                if (isLocationLine(line)) continue;
                if (isJobTitle(line)) { commit(); title = line; }
                else if (!desc) { desc = line; }
                continue;
            }

            // Before date: title then company
            if (!title) { title = line; }
            else if (!company) { company = line; }
            else if (!desc && line.length > 40) { desc = line; }
        }
        commit();
        return results.slice(0, 30);
    }";

    // Body-text education extractor for the new LinkedIn SDUI.
    private static readonly string EducationExtractorJs = @"() => {
        const clean = s => s.replace(/[\u200F\u200E\u200B\u200C\u200D\uFEFF]/g, '').trim();
        const normDigits = s => s ? s.replace(/[\u0660-\u0669]/g, d => String.fromCharCode(d.charCodeAt(0) - 0x0660 + 48)) : s;

        const STOP = new Set([
            '\u0627\u0644\u062e\u0628\u0631\u0629', 'Experience',
            '\u0627\u0644\u0645\u0647\u0627\u0631\u0627\u062a', 'Skills',
            '\u0645\u0632\u064a\u062f \u0645\u0646 \u0627\u0644\u0645\u0644\u0641\u0627\u062a \u0627\u0644\u0634\u062e\u0635\u064a\u0629 \u0645\u0646 \u0623\u062c\u0644\u0643',
            'More profiles for you', '\u0646\u0628\u0630\u0629 \u0639\u0646\u0627', 'About',
            '\u0625\u0645\u0643\u0627\u0646\u064a\u0629 \u0627\u0644\u0648\u0635\u0648\u0644', 'Accessibility',
            '\u062d\u0644\u0648\u0644 \u0627\u0644\u0645\u0648\u0627\u0647\u0628', 'Talent Solutions',
            '\u0625\u0631\u0634\u0627\u062f\u0627\u062a \u0627\u0644\u0645\u062c\u062a\u0645\u0639', 'Community Guidelines'
        ]);
        // Lines to skip that are clearly metadata, not school/degree
        const SKIP_LINE = new Set([
            '\u062a\u0645\u064a\u0632', '\u062a\u0641\u0648\u0642', 'Honors & Awards',
            '\u0645\u0634\u0631\u0648\u0639 \u0627\u0644\u062a\u062e\u0631\u062c', 'Graduation Project',
            '\u0639\u0646\u0648\u0627\u0646 \u0627\u0644\u0645\u0634\u0631\u0648\u0639', 'Project title',
            '\u0623\u0647\u062f\u0627\u0641 \u0627\u0644\u0645\u0634\u0631\u0648\u0639', 'Project Objectives',
            '\u062f\u0631\u062c\u0629 \u0645\u0634\u0631\u0648\u0639 \u0627\u0644\u062a\u062e\u0631\u062c', 'Graduation Project Degree',
            '\u2026 \u0627\u0644\u0645\u0632\u064a\u062f', '… المزيد'
        ]);

        const body = document.body.innerText || '';
        const lines = body.split('\n').map(clean).filter(l => l.length > 0);

        const startIdx = lines.findIndex(l => l === '\u0627\u0644\u062a\u0639\u0644\u064a\u0645' || l === 'Education');
        if (startIdx < 0) return [];

        const hasYear = l => /\d{4}|[\u0660-\u0669]{4}/.test(l);
        const isYearRange = l => hasYear(l) && /[\u2013\u2014\u2212\-–—]/.test(l);
        const isGrade = l => /^(Very good|Good|Excellent|\u062c\u064a\u062f \u062c\u062f\u064b\u0627|\u062c\u064a\u062f|\u0645\u0645\u062a\u0627\u0632|\u062a\u0642\u062f\u064a\u0631)/i.test(l);

        const results = [];
        let school = null, degree = null, fieldOfStudy = null, startYear = null, endYear = null;

        const commit = () => {
            if (school && school.length > 2) {
                results.push({
                    school: school.trim(),
                    degree: degree ? degree.trim() : null,
                    fieldOfStudy: fieldOfStudy ? fieldOfStudy.trim() : null,
                    startYear: normDigits(startYear),
                    endYear: normDigits(endYear)
                });
            }
            school = null; degree = null; fieldOfStudy = null; startYear = null; endYear = null;
        };

        // De-duplicate: track seen (school, year) pairs to avoid double entries
        const seen = new Set();

        for (let i = startIdx + 1; i < lines.length; i++) {
            const line = lines[i];
            if (!line) continue;
            if (STOP.has(line)) break;
            if (SKIP_LINE.has(line)) continue;
            if (line.startsWith('\u00b7') || line.startsWith('\u2022')) continue;
            if (line.startsWith('\u062a\u062c\u0631\u0628\u0629 Premium')) continue;
            // Skip long description lines (graduation project text etc.)
            if (line.length > 150) continue;

            // Year range line
            if (isYearRange(line)) {
                const parts = line.split(/\s*[\u2013\u2014\u2212\-–—]\s*/);
                startYear = parts[0]?.trim() || null;
                endYear = parts[1]?.trim() || null;
                continue;
            }

            // Grade line
            if (isGrade(line)) continue;

            // Degree/field line: contains '،' (Arabic comma) or 'Bachelor'/'Master'/'PhD' etc.
            if (line.includes('\u060c') || // Arabic comma ،
                /\b(Bachelor|Master|PhD|B\.Sc|M\.Sc|B\.A|M\.A|Doctor|Engineer|Diploma)\b/i.test(line)) {
                if (!degree) {
                    const parts = line.split(/\u060c|\u002c/);
                    degree = parts[0]?.trim() || line;
                    fieldOfStudy = parts[1]?.trim() || null;
                }
                continue;
            }

            // School name: if we already have a school and year, commit before starting new one
            if (school && startYear) {
                const key = school + '|' + startYear;
                if (!seen.has(key)) { seen.add(key); commit(); }
                else { school = null; degree = null; fieldOfStudy = null; startYear = null; endYear = null; }
            } else if (school && !startYear) {
                // Two school-looking lines without a year between them — duplicate school/college
                // Use the more specific (longer) one
                if (line.length > school.length) { school = line; degree = null; fieldOfStudy = null; }
                continue;
            } else {
                if (school) commit();
            }
            school = line;
        }
        if (school) {
            const key = school + '|' + (startYear || '');
            if (!seen.has(key)) { seen.add(key); commit(); }
        }
        return results.slice(0, 20);
    }";

    // Body-text skills extractor for the new LinkedIn SDUI.
    private static readonly string SkillsExtractorJs = @"() => {
        const clean = s => s.replace(/[\u200F\u200E\u200B\u200C\u200D\uFEFF]/g, '').trim();
        const normDigits = s => s ? s.replace(/[\u0660-\u0669]/g, d => String.fromCharCode(d.charCodeAt(0) - 0x0660 + 48)) : s;

        const STOP = new Set([
            '\u0627\u0644\u062e\u0628\u0631\u0629', 'Experience',
            '\u0627\u0644\u062a\u0639\u0644\u064a\u0645', 'Education',
            '\u0645\u0632\u064a\u062f \u0645\u0646 \u0627\u0644\u0645\u0644\u0641\u0627\u062a \u0627\u0644\u0634\u062e\u0635\u064a\u0629 \u0645\u0646 \u0623\u062c\u0644\u0643',
            'More profiles for you', '\u0646\u0628\u0630\u0629 \u0639\u0646\u0627', 'About',
            '\u0625\u0645\u0643\u0627\u0646\u064a\u0629 \u0627\u0644\u0648\u0635\u0648\u0644', 'Accessibility',
            '\u062d\u0644\u0648\u0644 \u0627\u0644\u0645\u0648\u0627\u0647\u0628', 'Talent Solutions',
            '\u0625\u0631\u0634\u0627\u062f\u0627\u062a \u0627\u0644\u0645\u062c\u062a\u0645\u0639', 'Community Guidelines'
        ]);
        // Category tabs to skip
        const SKIP_CATEGORY = new Set([
            '\u0627\u0644\u0643\u0644', 'All', '\u0627\u0644\u0645\u0639\u0631\u0641\u0629 \u0627\u0644\u0645\u0647\u0646\u064a\u0629', 'Industry Knowledge',
            '\u0627\u0644\u0623\u062f\u0648\u0627\u062a \u0648\u0627\u0644\u062a\u0642\u0646\u064a\u0627\u062a', 'Tools & Technologies',
            '\u0645\u0647\u0627\u0631\u0627\u062a \u0634\u062e\u0635\u064a\u0629 \u0648\u0628\u064a\u0646 \u0627\u0644\u0623\u0634\u062e\u0627\u0635', 'Interpersonal Skills',
            '\u0625\u0636\u0627\u0641\u0629 \u0645\u0647\u0627\u0631\u0629', 'Add a skill', 'Show all skills'
        ]);

        const body = document.body.innerText || '';
        const lines = body.split('\n').map(clean).filter(l => l.length > 0);

        const startIdx = lines.findIndex(l => l === '\u0627\u0644\u0645\u0647\u0627\u0631\u0627\u062a' || l === 'Skills');
        if (startIdx < 0) return [];

        const isEndorsementDetail = l =>
            l.startsWith('\u2066') || l.startsWith('\u2067') || // LRI/RLI marks
            l.startsWith('\u062a\u0645\u062a \u0627\u0644\u0645\u0635\u0627\u062f\u0642\u0629') || // تمت المصادقة
            l.startsWith('\u062a\u0645 \u0627\u0644\u062a\u0635\u062f\u064a\u0642');               // تم التصديق
        const isContextLine = l => l.includes(' \u0641\u064a ') && l.length > 30; // contains ' في '
        const isEndorsementCount = l => /^[\d\u0660-\u0669]+\s*\u0645\u0635\u0627\u062f\u0642\u0629/.test(l) || /^\d+\s+endorsement/i.test(l);

        const results = [];
        let currentSkill = null;
        let currentCount = 0;

        const commit = () => {
            if (currentSkill && currentSkill.length > 0 && currentSkill.length < 100) {
                results.push({ name: currentSkill.trim(), endorsementCount: currentCount });
            }
            currentSkill = null;
            currentCount = 0;
        };

        for (let i = startIdx + 1; i < lines.length; i++) {
            const line = lines[i];
            if (!line) continue;
            if (STOP.has(line)) break;
            if (SKIP_CATEGORY.has(line)) continue;
            if (line.startsWith('\u00b7') || line.startsWith('\u2022')) continue;
            if (line.startsWith('\u062a\u062c\u0631\u0628\u0629 Premium')) continue;
            if (isEndorsementDetail(line)) continue;
            if (isContextLine(line)) continue;

            if (isEndorsementCount(line)) {
                const match = line.match(/^[\d\u0660-\u0669]+/);
                if (match) currentCount = parseInt(normDigits(match[0])) || 0;
                continue;
            }

            // Otherwise it's a skill name
            commit();
            currentSkill = line;
        }
        commit();
        return results.slice(0, 80);
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
            'More profiles for you','المزيد من الملفات الشخصية',
            // Exact Arabic text that appears after projects section on the page:
            'مزيد من الملفات الشخصية من أجلك',
            // Footer section headings that signal end of content:
            'نبذة عنا','إمكانية الوصول','حلول المواهب','إرشادات المجتمع',
            'Accessibility','Talent Solutions','Community Guidelines']);
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

    // Languages body-text extractor.
    // LinkedIn /details/languages/ page renders: heading ("اللغات" or "Languages"),
    // then for each language: Name line, optional Proficiency line.
    private static readonly string LanguagesExtractorJs = @"() => {
        const clean = s => s.replace(/[\u200F\u200E\u200B\u200C\u200D\uFEFF]/g, '').trim();
        const body = document.body.innerText || '';
        const lines = body.split('\n').map(clean).filter(l => l.length > 0);

        const STOP = new Set([
            '\u0627\u0644\u062e\u0628\u0631\u0629', 'Experience',
            '\u0627\u0644\u062a\u0639\u0644\u064a\u0645', 'Education',
            '\u0627\u0644\u0645\u0647\u0627\u0631\u0627\u062a', 'Skills',
            '\u0645\u0632\u064a\u062f \u0645\u0646 \u0627\u0644\u0645\u0644\u0641\u0627\u062a \u0627\u0644\u0634\u062e\u0635\u064a\u0629 \u0645\u0646 \u0623\u062c\u0644\u0643',
            'More profiles for you',
            '\u0646\u0628\u0630\u0629 \u0639\u0646\u0627', 'About',
            '\u0625\u0645\u0643\u0627\u0646\u064a\u0629 \u0627\u0644\u0648\u0635\u0648\u0644', 'Accessibility',
            '\u062d\u0644\u0648\u0644 \u0627\u0644\u0645\u0648\u0627\u0647\u0628', 'Talent Solutions',
            '\u0625\u0631\u0634\u0627\u062f\u0627\u062a \u0627\u0644\u0645\u062c\u062a\u0645\u0639', 'Community Guidelines'
        ]);

        // Known proficiency level strings (Arabic + English)
        const PROFICIENCY = new Set([
            'Native or bilingual proficiency',
            'Full professional proficiency',
            'Professional working proficiency',
            'Limited working proficiency',
            'Elementary proficiency',
            '\u0625\u062a\u0642\u0627\u0646 \u0644\u063a\u0629 \u0627\u0644\u0623\u0645 \u0623\u0648 \u062b\u0646\u0627\u0626\u064a \u0627\u0644\u0644\u063a\u0629',
            '\u0625\u062a\u0642\u0627\u0646 \u0645\u0647\u0646\u064a \u0643\u0627\u0645\u0644',
            '\u0625\u062a\u0642\u0627\u0646 \u0645\u0647\u0646\u064a \u0639\u0645\u0644\u064a',
            '\u0625\u062a\u0642\u0627\u0646 \u0639\u0645\u0644\u064a \u0645\u062d\u062f\u0648\u062f',
            '\u0625\u062a\u0642\u0627\u0646 \u0627\u0628\u062a\u062f\u0627\u0626\u064a'
        ]);

        const startIdx = lines.findIndex(l => l === '\u0627\u0644\u0644\u063a\u0627\u062a' || l === 'Languages');
        if (startIdx < 0) return [];

        const results = [];
        let langName = null;

        const commit = (proficiency) => {
            if (langName && langName.length > 0 && langName.length < 60) {
                results.push({ name: langName.trim(), proficiency: proficiency || null });
            }
            langName = null;
        };

        for (let i = startIdx + 1; i < lines.length; i++) {
            const line = lines[i];
            if (!line) continue;
            if (STOP.has(line)) break;
            if (line.startsWith('\u00b7') || line.startsWith('\u2022')) continue;
            if (line.startsWith('\u062a\u062c\u0631\u0628\u0629 Premium')) continue;
            // Skip 'Add a language' / 'أضف لغة'
            if (line.startsWith('Add a language') || line.startsWith('\u0623\u0636\u0641 \u0644\u063a\u0629')) continue;

            if (PROFICIENCY.has(line)) {
                commit(line);
            } else {
                // New language name — commit previous without proficiency
                if (langName) commit(null);
                langName = line;
            }
        }
        commit(null);
        return results.slice(0, 30);
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
