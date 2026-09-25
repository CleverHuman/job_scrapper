using System.Net;
using System.Text.RegularExpressions;

namespace JobrightScraper.Services;

internal static partial class HtmlText
{
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutScripts = ScriptStyleRegex().Replace(html, " ");
        var withBreaks = BlockTagRegex().Replace(withoutScripts, "\n");
        var withoutTags = TagRegex().Replace(withBreaks, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var normalized = WhitespaceRegex().Replace(decoded, " ").Trim();
        return normalized;
    }

    [GeneratedRegex(@"<(script|style)\b[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<(br|p|div|li|h[1-6]|tr|section)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
