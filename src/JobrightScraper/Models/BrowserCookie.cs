namespace JobrightScraper.Models;

public sealed class BrowserCookie
{
    public required string Name { get; init; }
    public required string Value { get; init; }
    public string Domain { get; init; } = ".jobright.ai";
    public string Path { get; init; } = "/";
    public bool HttpOnly { get; init; }
    public bool Secure { get; init; }
    public DateTimeOffset? Expires { get; init; }
}
