using System.Net.Http;
using System.Text.Json;
using JobrightScraper.Models;

namespace JobrightScraper.Services;

public sealed class JobScraperService : IJobScraper
{
    private readonly JobrightApiClient _api = new();
    private readonly IJobStore _store;

    public JobScraperService(IJobStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<JobListing>> ScrapeAsync(
        IBrowserSession browser,
        SearchFilters filters,
        IProgress<ScrapeProgress>? progress,
        IProgress<JobListing>? jobFound,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(filters);

        if (!browser.IsReady)
        {
            throw new InvalidOperationException("WebView2 is not ready. Open the Browser tab first.");
        }

        var cookies = await browser.GetJobrightCookiesAsync();
        progress?.Report(new ScrapeProgress(0, filters.MaxJobs, $"Using cookies: {SessionCookieHelper.Describe(cookies)}"));

        try
        {
            var fromApi = await _api.FetchRecommendationsAsync(cookies, filters, _store, progress, jobFound, cancellationToken);
            if (fromApi.Count > 0)
            {
                progress?.Report(new ScrapeProgress(fromApi.Count, fromApi.Count, $"Finished ({fromApi.Count} new jobs)"));
                return fromApi.OrderByDescending(job => job.PostedAtUtc ?? DateTime.MinValue).ToList();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress?.Report(new ScrapeProgress(0, filters.MaxJobs, $"API scrape missed ({ex.Message}). Trying the open browser…"));
        }

        var fromPage = await FetchViaWebViewAsync(browser, cookies, filters, progress, jobFound, cancellationToken);
        if (fromPage.Count > 0)
        {
            progress?.Report(new ScrapeProgress(fromPage.Count, fromPage.Count, $"Finished ({fromPage.Count} new jobs)"));
            return fromPage.OrderByDescending(job => job.PostedAtUtc ?? DateTime.MinValue).ToList();
        }

        throw new InvalidOperationException(
            "No new remote jobs from the last 24 hours were found. LinkedIn apply links and jobs already in the database are skipped.");
    }

    private async Task<List<JobListing>> FetchViaWebViewAsync(
        IBrowserSession browser,
        IReadOnlyList<BrowserCookie> cookies,
        SearchFilters filters,
        IProgress<ScrapeProgress>? progress,
        IProgress<JobListing>? jobFound,
        CancellationToken cancellationToken)
    {
        var results = new List<JobListing>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var client = JobrightApiClient.CreateClient(cookies);

        for (var position = 0; results.Count < filters.MaxJobs && position < 600; position += 20)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ScrapeProgress(results.Count, filters.MaxJobs, "Reading jobs from the logged-in browser"));

            var refresh = position == 0 ? "&refresh=true" : string.Empty;
            var url = $"{AppPaths.RecommendApiUrl}?position={position}&count=20{refresh}";
            var payload = await FetchJsonFromPageAsync(browser, url, cancellationToken);
            if (payload is null)
            {
                break;
            }

            IReadOnlyList<JobListing> page;
            try
            {
                page = JobrightJson.ParseRecommendResponse(payload);
            }
            catch (Exception)
            {
                break;
            }

            if (page.Count == 0)
            {
                break;
            }

            foreach (var job in page)
            {
                if (await TryAcceptAsync(client, job, filters, seen, results, progress, jobFound, cancellationToken))
                {
                    if (results.Count >= filters.MaxJobs)
                    {
                        break;
                    }
                }
            }

            if (page.Count < 20)
            {
                break;
            }

            await Task.Delay(400, cancellationToken);
        }

        return results;
    }

    private async Task<bool> TryAcceptAsync(
        HttpClient client,
        JobListing job,
        SearchFilters filters,
        HashSet<string> seen,
        List<JobListing> results,
        IProgress<ScrapeProgress>? progress,
        IProgress<JobListing>? jobFound,
        CancellationToken cancellationToken)
    {
        if (!JobrightJson.Matches(job, filters))
        {
            return false;
        }

        var key = string.IsNullOrWhiteSpace(job.JobId) ? $"{job.Company}|{job.Title}" : job.JobId;
        if (!seen.Add(key) || _store.Exists(job.JobId, job.Url))
        {
            return false;
        }

        progress?.Report(new ScrapeProgress(results.Count + 1, filters.MaxJobs, $"Resolving apply link for {job.Title}"));
        await ApplyUrlResolver.ResolveAsync(client, job, cancellationToken);

        if (!ApplyUrlResolver.IsCompanyUrl(job.Url) || JobRules.IsLinkedInApply(job.Url) || _store.Exists(job.JobId, job.Url))
        {
            return false;
        }

        _store.Upsert(job);
        results.Add(job);
        jobFound?.Report(job);
        return true;
    }

    private static async Task<string?> FetchJsonFromPageAsync(
        IBrowserSession browser,
        string url,
        CancellationToken cancellationToken)
    {
        var startScript = $$"""
            (function() {
              window.__jrFetch = { done: false, status: 0, body: null, error: null };
              fetch({{JsonSerializer.Serialize(url)}}, {
                credentials: 'include',
                headers: { 'Accept': 'application/json, text/plain, */*' }
              }).then(function(r) {
                return r.text().then(function(t) {
                  window.__jrFetch = { done: true, status: r.status, body: t, error: null };
                });
              }).catch(function(e) {
                window.__jrFetch = { done: true, status: 0, body: null, error: null };
              });
            })();
            """;

        await browser.ExecuteScriptAsync(startScript);

        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(250, cancellationToken);
            var raw = await browser.ExecuteScriptAsync("window.__jrFetch || null");
            if (string.IsNullOrWhiteSpace(raw) || raw == "null")
            {
                continue;
            }

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (!root.TryGetProperty("done", out var done) || done.ValueKind is not JsonValueKind.True)
            {
                continue;
            }

            if (root.TryGetProperty("status", out var status)
                && status.TryGetInt32(out var code)
                && code is 401 or 403)
            {
                throw new InvalidOperationException("The open browser is not authenticated with Jobright anymore.");
            }

            return root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
                ? body.GetString()
                : null;
        }

        return null;
    }
}
