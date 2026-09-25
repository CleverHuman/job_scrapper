using JobrightScraper.Models;

namespace JobrightScraper.Services;

internal static class SessionCookieHelper
{
    private static readonly string[] SessionNameHints =
    [
        "session_id", "session", "token", "auth", "sid", "jwt", "access", "login", "user"
    ];

    public static bool LooksLoggedIn(IReadOnlyList<BrowserCookie> cookies)
    {
        if (cookies.Count == 0)
        {
            return false;
        }

        return cookies.Any(cookie =>
            cookie.Name.Equals("SESSION_ID", StringComparison.OrdinalIgnoreCase)
            || SessionNameHints.Any(hint =>
                cookie.Name.Contains(hint, StringComparison.OrdinalIgnoreCase)));
    }

    public static string Describe(IReadOnlyList<BrowserCookie> cookies)
    {
        if (cookies.Count == 0)
        {
            return "no cookies";
        }

        var names = cookies.Select(cookie => cookie.Name).Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", names);
    }
}
