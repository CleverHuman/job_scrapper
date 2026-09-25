using System.Globalization;
using System.Text.RegularExpressions;

namespace JobrightScraper.Services;

internal static partial class PostedTimeParser
{
    public static DateTime? Parse(string? relative, long? unixTimestamp)
    {
        if (unixTimestamp is long value and > 0)
        {
            try
            {
                var dto = value > 10_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                    : DateTimeOffset.FromUnixTimeSeconds(value);
                return dto.UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        if (string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        var text = relative.Trim();
        var now = DateTime.UtcNow;

        if (text.Equals("just now", StringComparison.OrdinalIgnoreCase)
            || text.Equals("now", StringComparison.OrdinalIgnoreCase)
            || text.Equals("today", StringComparison.OrdinalIgnoreCase)
            || text.Contains("minute", StringComparison.OrdinalIgnoreCase))
        {
            var minutes = NumberRegex().Match(text);
            return minutes.Success && int.TryParse(minutes.Value, out var count)
                ? now.AddMinutes(-count)
                : now;
        }

        if (text.Contains("hour", StringComparison.OrdinalIgnoreCase))
        {
            var hours = NumberRegex().Match(text);
            return hours.Success && int.TryParse(hours.Value, out var count)
                ? now.AddHours(-count)
                : now.AddHours(-1);
        }

        if (text.Equals("yesterday", StringComparison.OrdinalIgnoreCase))
        {
            return now.AddDays(-1);
        }

        if (text.Contains("day", StringComparison.OrdinalIgnoreCase))
        {
            var days = NumberRegex().Match(text);
            return days.Success && int.TryParse(days.Value, out var count)
                ? now.AddDays(-count)
                : now.AddDays(-1);
        }

        if (text.Contains("week", StringComparison.OrdinalIgnoreCase))
        {
            var weeks = NumberRegex().Match(text);
            return weeks.Success && int.TryParse(weeks.Value, out var count)
                ? now.AddDays(-7 * count)
                : now.AddDays(-7);
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }

    public static bool IsWithinHours(DateTime? postedAtUtc, int hours)
    {
        if (postedAtUtc is null)
        {
            return false;
        }

        return postedAtUtc.Value >= DateTime.UtcNow.AddHours(-hours);
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();
}
