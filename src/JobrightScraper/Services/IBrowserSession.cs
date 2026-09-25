using JobrightScraper.Models;

namespace JobrightScraper.Services;

public interface IBrowserSession : ICookieBridge
{
    string? CurrentUrl { get; }

    Task<string> ExecuteScriptAsync(string javaScript);
}
