namespace JobrightScraper.Models;

public sealed class SearchFilters
{
    public string Keywords { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public WorkMode WorkMode { get; init; } = WorkMode.All;
    public int MinMatchScore { get; init; }
    public int MaxJobs { get; init; } = 50;
}
