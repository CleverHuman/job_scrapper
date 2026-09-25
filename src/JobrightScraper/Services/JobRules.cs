namespace JobrightScraper.Services;

internal static class JobRules
{
    public static bool IsLinkedInApply(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)
               || url.Contains("lnkd.in", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeJobType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim();
        if (ContainsAny(text, "full"))
        {
            return "Full-time";
        }

        if (ContainsAny(text, "part"))
        {
            return "Part-time";
        }

        if (ContainsAny(text, "contract", "contractor", "freelance"))
        {
            return "Contract";
        }

        if (ContainsAny(text, "intern"))
        {
            return "Internship";
        }

        if (ContainsAny(text, "temp"))
        {
            return "Temporary";
        }

        return text;
    }

    private static bool ContainsAny(string text, params string[] tokens) =>
        tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
}
