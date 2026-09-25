using JobrightScraper.Models;

namespace JobrightScraper.Services;

public interface IJobScraper
{
    Task<IReadOnlyList<JobListing>> ScrapeAsync(
        IReadOnlyList<BrowserCookie> cookies,
        SearchFilters filters,
        IProgress<ScrapeProgress>? progress,
        IProgress<JobListing>? jobFound,
        CancellationToken cancellationToken);
}
