using JobrightScraper.Models;
using Microsoft.Playwright;

namespace JobrightScraper.Services;

public sealed class PlaywrightJobScraper : IJobScraper
{
    private static readonly TimeSpan DetailDelay = TimeSpan.FromMilliseconds(800);

    public async Task<IReadOnlyList<JobListing>> ScrapeAsync(
        IReadOnlyList<BrowserCookie> cookies,
        SearchFilters filters,
        IProgress<ScrapeProgress>? progress,
        IProgress<JobListing>? jobFound,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(filters);

        if (cookies.Count == 0)
        {
            throw new InvalidOperationException("No Jobright cookies were found. Log in on the Browser tab first.");
        }

        progress?.Report(new ScrapeProgress(0, filters.MaxJobs, "Starting Playwright"));

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
            });

            await context.AddCookiesAsync(cookies.Select(ToPlaywrightCookie));
            var page = await context.NewPageAsync();

            progress?.Report(new ScrapeProgress(0, filters.MaxJobs, "Opening Jobright recommend feed"));
            await page.GotoAsync(AppPaths.RecommendUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });
            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });
            }
            catch (PlaywrightException)
            {
                // Feed may keep streaming; continue with whatever is already rendered.
            }

            cancellationToken.ThrowIfCancellationRequested();
            await EnsureAuthenticatedAsync(page);
            await ApplySiteFiltersAsync(page, filters, cancellationToken);

            var listed = await CollectListingsAsync(page, filters, progress, cancellationToken);
            var results = new List<JobListing>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var listing in listed)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(listing.Url) || !seen.Add(listing.Url))
                {
                    continue;
                }

                progress?.Report(new ScrapeProgress(results.Count + 1, listed.Count, $"Opening {listing.Title}"));
                var detailed = await ScrapeDetailAsync(page, listing, cancellationToken);
                if (detailed.MatchScore is int score && score < filters.MinMatchScore)
                {
                    continue;
                }

                results.Add(detailed);
                jobFound?.Report(detailed);
                await page.WaitForTimeoutAsync((float)DetailDelay.TotalMilliseconds);
            }

            progress?.Report(new ScrapeProgress(results.Count, results.Count, $"Finished ({results.Count} jobs)"));
            return results;
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
                                            || ex.Message.Contains("Browser was not found", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Playwright Chromium is not installed. From the project output folder run: pwsh playwright.ps1 install chromium",
                ex);
        }
    }

    private static async Task EnsureAuthenticatedAsync(IPage page)
    {
        var url = page.Url;
        if (url.Contains("login", StringComparison.OrdinalIgnoreCase)
            || url.Contains("signup", StringComparison.OrdinalIgnoreCase)
            || url.Contains("onboarding", StringComparison.OrdinalIgnoreCase)
            || url.Contains("signin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Jobright redirected to login. Complete sign-in on the Browser tab, then click I'm logged in.");
        }

        var body = await page.InnerTextAsync("body");
        if (body.Contains("Sign in", StringComparison.OrdinalIgnoreCase)
            && body.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("/jobs/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The Jobright session expired. Log in again on the Browser tab.");
        }
    }

    private static async Task ApplySiteFiltersAsync(IPage page, SearchFilters filters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(filters.Keywords))
        {
            await TryFillAsync(page, JobrightSelectors.SearchInput, filters.Keywords);
        }

        if (!string.IsNullOrWhiteSpace(filters.Location))
        {
            await TryFillAsync(page, JobrightSelectors.LocationInput, filters.Location);
        }

        if (filters.WorkMode is not WorkMode.All)
        {
            await TryClickLabelAsync(page, filters.WorkMode.ToString());
        }

        await page.WaitForTimeoutAsync(1200);
    }

    private static async Task TryFillAsync(IPage page, string selector, string value)
    {
        try
        {
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync() == 0)
            {
                return;
            }

            await locator.ClickAsync(new LocatorClickOptions { Timeout = 4000 });
            await locator.FillAsync(value, new LocatorFillOptions { Timeout = 4000 });
            await locator.PressAsync("Enter");
        }
        catch (PlaywrightException)
        {
            // Site chrome changed; keep scraping what is visible.
        }
    }

    private static async Task TryClickLabelAsync(IPage page, string label)
    {
        try
        {
            var locator = page.Locator(JobrightSelectors.WorkModeControl(label)).First;
            if (await locator.CountAsync() == 0)
            {
                return;
            }

            await locator.ClickAsync(new LocatorClickOptions { Timeout = 4000 });
        }
        catch (PlaywrightException)
        {
        }
    }

    private static async Task<List<JobListing>> CollectListingsAsync(
        IPage page,
        SearchFilters filters,
        IProgress<ScrapeProgress>? progress,
        CancellationToken cancellationToken)
    {
        var jobs = new Dictionary<string, JobListing>(StringComparer.OrdinalIgnoreCase);
        var stagnantRounds = 0;

        for (var round = 0; round < 25 && jobs.Count < filters.MaxJobs; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = await page.EvaluateAsync<List<JobCardDto>>(JobrightSelectors.CollectJobsScript);
            var added = 0;

            foreach (var card in batch ?? [])
            {
                if (string.IsNullOrWhiteSpace(card.Url) || jobs.ContainsKey(card.Url))
                {
                    continue;
                }

                if (card.MatchScore is int score && score < filters.MinMatchScore)
                {
                    continue;
                }

                jobs[card.Url] = card.ToListing();
                added++;
                if (jobs.Count >= filters.MaxJobs)
                {
                    break;
                }
            }

            progress?.Report(new ScrapeProgress(jobs.Count, filters.MaxJobs, "Scrolling job feed"));

            if (added == 0)
            {
                stagnantRounds++;
            }
            else
            {
                stagnantRounds = 0;
            }

            if (stagnantRounds >= 3)
            {
                break;
            }

            await page.EvaluateAsync(JobrightSelectors.ScrollFeedScript);
            await page.WaitForTimeoutAsync(900);
        }

        if (jobs.Count == 0)
        {
            throw new InvalidOperationException(
                "No job cards were found. Confirm you are logged in and that the recommend feed is visible, then try again.");
        }

        return jobs.Values.Take(filters.MaxJobs).ToList();
    }

    private static async Task<JobListing> ScrapeDetailAsync(IPage page, JobListing listing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await page.GotoAsync(listing.Url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });
            await page.WaitForTimeoutAsync(700);

            var detail = await page.EvaluateAsync<JobCardDto>(JobrightSelectors.ExtractDetailScript);
            if (detail is null)
            {
                return listing;
            }

            listing.Title = FirstNonEmpty(detail.Title, listing.Title);
            listing.Company = FirstNonEmpty(detail.Company, listing.Company);
            listing.Location = FirstNonEmpty(detail.Location, listing.Location);
            listing.WorkMode = FirstNonEmpty(detail.WorkMode, listing.WorkMode);
            listing.MatchScore = detail.MatchScore ?? listing.MatchScore;
            listing.Salary = FirstNonEmpty(detail.Salary, listing.Salary);
            listing.PostedAt = FirstNonEmpty(detail.PostedAt, listing.PostedAt);
            listing.Description = HtmlText.ToPlainText(detail.Description);
            listing.Requirements = HtmlText.ToPlainText(detail.Requirements);
            listing.Skills = HtmlText.ToPlainText(detail.Skills);
            listing.ScrapedAt = DateTime.UtcNow;
            return listing;
        }
        catch (PlaywrightException)
        {
            return listing;
        }
    }

    private static Cookie ToPlaywrightCookie(BrowserCookie cookie)
    {
        var mapped = new Cookie
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = cookie.Domain.Trim(),
            Path = cookie.Path,
            HttpOnly = cookie.HttpOnly,
            Secure = cookie.Secure
        };

        if (cookie.Expires is { } expires)
        {
            mapped.Expires = expires.ToUnixTimeSeconds();
        }

        return mapped;
    }

    private static string FirstNonEmpty(string? preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();

    private sealed class JobCardDto
    {
        public string? Title { get; set; }
        public string? Company { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public int? MatchScore { get; set; }
        public string? Salary { get; set; }
        public string? PostedAt { get; set; }
        public string? Url { get; set; }
        public string? Description { get; set; }
        public string? Requirements { get; set; }
        public string? Skills { get; set; }

        public JobListing ToListing() => new()
        {
            Title = Title?.Trim() ?? string.Empty,
            Company = Company?.Trim() ?? string.Empty,
            Location = Location?.Trim() ?? string.Empty,
            WorkMode = WorkMode?.Trim() ?? string.Empty,
            MatchScore = MatchScore,
            Salary = Salary?.Trim() ?? string.Empty,
            PostedAt = PostedAt?.Trim() ?? string.Empty,
            Url = Url?.Trim() ?? string.Empty,
            ScrapedAt = DateTime.UtcNow
        };
    }
}
