namespace JobrightScraper.Models;

public sealed class SearchFilters
{
    public string Keywords { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public WorkMode WorkMode { get; init; } = WorkMode.Remote;
    public int MaxJobs { get; init; } = 50;
    public int MaxAgeHours { get; init; } = 24;
}
