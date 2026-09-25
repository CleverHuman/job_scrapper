using JobrightScraper.Models;
using Microsoft.Web.WebView2.Core;

namespace JobrightScraper.Services;

public sealed class WebViewCookieBridge : ICookieBridge
{
    private CoreWebView2? _core;

    public bool IsReady => _core is not null;

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

        var cookies = await _core.CookieManager.GetCookiesAsync(AppPaths.JobrightOrigin);
        return cookies.Select(ToBrowserCookie).ToList();
    }

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
