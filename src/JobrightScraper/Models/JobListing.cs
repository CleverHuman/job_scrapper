namespace JobrightScraper.Models;

public sealed class JobListing
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public int? MatchScore { get; set; }
    public string Salary { get; set; } = string.Empty;
    public string PostedAt { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;

    public string DescriptionPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                return string.Empty;
            }

            return Description.Length <= 120
                ? Description
                : Description[..117] + "...";
        }
    }
}
