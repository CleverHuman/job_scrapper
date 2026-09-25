using System.Net;
using System.Net.Http;
using System.Text.Json;
using JobrightScraper.Models;

namespace JobrightScraper.Services;

public sealed class JobrightApiClient
{
    private const int PageSize = 20;

    public async Task<IReadOnlyList<JobListing>> FetchRecommendationsAsync(
        IReadOnlyList<BrowserCookie> cookies,
        SearchFilters filters,
        IJobStore store,
        IProgress<ScrapeProgress>? progress,
        IProgress<JobListing>? jobFound,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(cookies);

        var results = new List<JobListing>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var position = 0; results.Count < filters.MaxJobs && position < PageSize * 30; position += PageSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ScrapeProgress(results.Count, filters.MaxJobs, "Fetching Jobright recommendations"));

            var url = $"{AppPaths.RecommendApiUrl}?position={position}&count={PageSize}";
            if (position == 0)
            {
                url += "&refresh=true";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Origin", AppPaths.JobrightOrigin);
            request.Headers.TryAddWithoutValidation("Referer", AppPaths.RecommendUrl);
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(
                    "Jobright rejected the session (401/403). Stay on the recommend feed in the Browser tab and click I'm logged in, then scrape again.");
            }

            response.EnsureSuccessStatusCode();

            IReadOnlyList<JobListing> page;
            try
            {
                page = JobrightJson.ParseRecommendResponse(body);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Jobright returned a response that was not JSON. Sign in again on the Browser tab.", ex);
            }

            if (page.Count == 0)
            {
                break;
            }

            foreach (var job in page)
            {
                if (!JobrightJson.Matches(job, filters))
                {
                    continue;
                }

                var key = string.IsNullOrWhiteSpace(job.JobId) ? $"{job.Company}|{job.Title}" : job.JobId;
                if (!seen.Add(key) || store.Exists(job.JobId, job.Url))
                {
                    continue;
                }

                progress?.Report(new ScrapeProgress(results.Count + 1, filters.MaxJobs, $"Resolving apply link for {job.Title}"));
                await ApplyUrlResolver.ResolveAsync(client, job, cancellationToken);

                if (!ApplyUrlResolver.IsCompanyUrl(job.Url) || JobRules.IsLinkedInApply(job.Url) || store.Exists(job.JobId, job.Url))
                {
                    continue;
                }

                store.Upsert(job);
                results.Add(job);
                jobFound?.Report(job);
                if (results.Count >= filters.MaxJobs)
                {
                    break;
                }
            }

            if (page.Count < PageSize)
            {
                break;
            }

            await Task.Delay(800, cancellationToken);
        }

        return results;
    }

    internal static HttpClient CreateClient(IReadOnlyList<BrowserCookie> cookies)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 8,
            UseCookies = true,
            CookieContainer = BuildCookieContainer(cookies)
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        return client;
    }

    private static CookieContainer BuildCookieContainer(IReadOnlyList<BrowserCookie> cookies)
    {
        var container = new CookieContainer();
        foreach (var cookie in cookies)
        {
            try
            {
                var host = cookie.Domain.Trim().TrimStart('.');
                if (string.IsNullOrWhiteSpace(host))
                {
                    host = "jobright.ai";
                }

                container.Add(new Uri($"https://{host}/"), new Cookie(cookie.Name, cookie.Value, cookie.Path, host)
                {
                    Secure = cookie.Secure,
                    HttpOnly = cookie.HttpOnly
                });
            }
            catch (CookieException)
            {
                // Skip cookies the container will not accept; SESSION_ID still goes through when valid.
            }
        }

        return container;
    }
}
