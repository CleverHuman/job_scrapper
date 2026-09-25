using System.IO;

namespace JobrightScraper.Services;

internal static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JobrightScraper");

    public static string WebView2UserData { get; } = Path.Combine(Root, "WebView2");

    public static string Database { get; } = @"D:\jobs.db";

    public const string JobrightOrigin = "https://jobright.ai";
    public const string SwanApiOrigin = "https://swan-api.jobright.ai";
    public const string RecommendUrl = "https://jobright.ai/jobs/recommend";
    public const string RecommendApiUrl = "https://swan-api.jobright.ai/swan/recommend/list/jobs";
}
