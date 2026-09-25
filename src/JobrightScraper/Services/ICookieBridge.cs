using JobrightScraper.Models;

namespace JobrightScraper.Services;

public interface ICookieBridge
{
    bool IsReady { get; }

    Task<IReadOnlyList<BrowserCookie>> GetJobrightCookiesAsync();
}
