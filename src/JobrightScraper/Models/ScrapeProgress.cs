namespace JobrightScraper.Models;

public sealed record ScrapeProgress(int Current, int Total, string Message);
