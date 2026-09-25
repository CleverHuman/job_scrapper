using JobrightScraper.Models;
using Microsoft.Web.WebView2.Core;

namespace JobrightScraper.Services;

public sealed class WebViewCookieBridge : IBrowserSession
{
    private CoreWebView2? _core;

    public bool IsReady => _core is not null;

    public string? CurrentUrl => _core?.Source;

    public void Attach(CoreWebView2 coreWebView)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);
        _core = coreWebView;
    }

    public async Task<IReadOnlyList<BrowserCookie>> GetJobrightCookiesAsync()
    {
        if (_core is null)
        {
            throw new InvalidOperationException("WebView2 is not ready yet. Open the Browser tab first.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cookies = new List<BrowserCookie>();

        foreach (var uri in new[] { string.Empty, AppPaths.JobrightOrigin, AppPaths.SwanApiOrigin })
        {
            IReadOnlyList<CoreWebView2Cookie> batch;
            try
            {
                batch = await _core.CookieManager.GetCookiesAsync(uri);
            }
            catch (ArgumentException)
            {
                continue;
            }

            foreach (var cookie in batch)
            {
                if (!IsJobrightCookie(cookie) || !seen.Add($"{cookie.Domain}|{cookie.Name}|{cookie.Path}"))
                {
                    continue;
                }

                cookies.Add(ToBrowserCookie(cookie));
            }
        }

        return cookies;
    }

    public async Task<string> ExecuteScriptAsync(string javaScript)
    {
        if (_core is null)
        {
            throw new InvalidOperationException("WebView2 is not ready yet. Open the Browser tab first.");
        }

        return await _core.ExecuteScriptAsync(javaScript);
    }

    private static bool IsJobrightCookie(CoreWebView2Cookie cookie) =>
        cookie.Domain.Contains("jobright.ai", StringComparison.OrdinalIgnoreCase);

    private static BrowserCookie ToBrowserCookie(CoreWebView2Cookie cookie)
    {
        DateTimeOffset? expires = null;
        if (cookie.Expires is { Year: > 1 })
        {
            var utc = cookie.Expires.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(cookie.Expires, DateTimeKind.Utc)
                : cookie.Expires.ToUniversalTime();
            expires = new DateTimeOffset(utc);
        }

        return new BrowserCookie
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = string.IsNullOrWhiteSpace(cookie.Domain) ? ".jobright.ai" : cookie.Domain,
            Path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
            HttpOnly = cookie.IsHttpOnly,
            Secure = cookie.IsSecure,
            Expires = expires
        };
    }
}
