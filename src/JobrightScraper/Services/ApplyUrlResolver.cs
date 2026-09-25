using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobrightScraper.Models;

namespace JobrightScraper.Services;

internal static partial class ApplyUrlResolver
{
    public static async Task ResolveAsync(HttpClient client, JobListing job, CancellationToken cancellationToken)
    {
        var candidate = job.Url;
        if (IsCompanyUrl(candidate))
        {
            job.Url = candidate;
            return;
        }

        if (!string.IsNullOrWhiteSpace(job.JobId))
        {
            candidate = FirstCompanyUrl(
                candidate,
                await FetchApplyFromDetailApiAsync(client, job.JobId, cancellationToken),
                await FetchApplyFromJobPageAsync(client, job.JobId, cancellationToken));
        }

        if (!IsCompanyUrl(candidate) && LooksLikeHttpUrl(candidate))
        {
            candidate = await FollowRedirectsAsync(client, candidate, cancellationToken) ?? candidate;
        }

        job.Url = IsCompanyUrl(candidate) ? candidate! : string.Empty;
    }

    private static async Task<string?> FetchApplyFromDetailApiAsync(
        HttpClient client,
        string jobId,
        CancellationToken cancellationToken)
    {
        foreach (var url in DetailApiUrls(jobId))
        {
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (TryFindApplyUrl(body, out var apply) && IsCompanyUrl(apply))
                {
                    return apply;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        return null;
    }

    private static async Task<string?> FetchApplyFromJobPageAsync(
        HttpClient client,
        string jobId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync($"{AppPaths.JobrightOrigin}/jobs/info/{jobId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (TryFindApplyUrl(html, out var apply) && IsCompanyUrl(apply))
            {
                return apply;
            }
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return null;
    }

    private static async Task<string?> FollowRedirectsAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            var final = response.RequestMessage?.RequestUri?.ToString();
            if (IsCompanyUrl(final))
            {
                return final;
            }

            if (response.Headers.Location is { } location)
            {
                var absolute = location.IsAbsoluteUri ? location.ToString() : new Uri(new Uri(url), location).ToString();
                if (IsCompanyUrl(absolute))
                {
                    return absolute;
                }
            }
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return null;
    }

    public static bool TryFindApplyUrl(string payload, out string applyUrl)
    {
        applyUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        foreach (Match match in ApplyJsonFieldRegex().Matches(payload))
        {
            var value = DecodeJsonString(match.Groups[1].Value);
            if (IsCompanyUrl(value))
            {
                applyUrl = value;
                return true;
            }
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (FindApplyUrl(document.RootElement) is { } found && IsCompanyUrl(found))
            {
                applyUrl = found;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    public static string? FindApplyUrl(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String
                        && property.Name.Contains("apply", StringComparison.OrdinalIgnoreCase)
                        && IsCompanyUrl(property.Value.GetString()))
                    {
                        return property.Value.GetString();
                    }

                    if (FindApplyUrl(property.Value) is { } nested)
                    {
                        return nested;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (FindApplyUrl(item) is { } nested)
                    {
                        return nested;
                    }
                }

                break;
        }

        return null;
    }

    public static bool IsCompanyUrl(string? url)
    {
        if (!LooksLikeHttpUrl(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        return !host.EndsWith("jobright.ai", StringComparison.OrdinalIgnoreCase)
               && !host.Equals("jobright.ai", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeHttpUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static string? FirstCompanyUrl(params string?[] candidates) =>
        candidates.FirstOrDefault(IsCompanyUrl);

    private static IEnumerable<string> DetailApiUrls(string jobId)
    {
        var encoded = Uri.EscapeDataString(jobId);
        yield return $"{AppPaths.SwanApiOrigin}/swan/job/info?jobId={encoded}";
        yield return $"{AppPaths.SwanApiOrigin}/swan/job?jobId={encoded}";
        yield return $"{AppPaths.SwanApiOrigin}/swan/recommend/job?jobId={encoded}";
        yield return $"{AppPaths.SwanApiOrigin}/swan/jobs/info/{encoded}";
        yield return $"{AppPaths.SwanApiOrigin}/swan/apply/link?jobId={encoded}";
    }

    private static string DecodeJsonString(string value) =>
        value.Replace("\\/", "/").Replace("\\u0026", "&").Replace("\\\"", "\"");

    [GeneratedRegex("""apply(?:Link|Url|URL|_link|_url)"\s*:\s*"([^"]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ApplyJsonFieldRegex();
}
