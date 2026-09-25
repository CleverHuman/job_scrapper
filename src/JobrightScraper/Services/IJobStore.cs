using JobrightScraper.Models;

namespace JobrightScraper.Services;

public interface IJobStore
{
    bool Exists(string? jobId, string? url);

    void Upsert(JobListing job);
}
