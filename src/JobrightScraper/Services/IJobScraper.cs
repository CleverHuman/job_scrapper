using JobrightScraper.Models;

namespace JobrightScraper.Services;

public interface IJobScraper
{
    Task<IReadOnlyList<JobListing>> ScrapeAsync(
        IBrowserSession browser,
        SearchFilters filters,
        IProgress<ScrapeProgress>? progress,
        IProgress<JobListing>? jobFound,
        CancellationToken cancellationToken);
}
