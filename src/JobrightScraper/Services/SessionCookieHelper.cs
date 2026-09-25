using JobrightScraper.Models;

namespace JobrightScraper.Services;

internal static class SessionCookieHelper
{
    private static readonly string[] SessionNameHints =
    [
        "session", "token", "auth", "sid", "jwt", "access", "login", "user"
    ];

    public static bool LooksLoggedIn(IReadOnlyList<BrowserCookie> cookies)
    {
        if (cookies.Count == 0)
        {
            return false;
        }

        return cookies.Any(cookie =>
            SessionNameHints.Any(hint =>
                cookie.Name.Contains(hint, StringComparison.OrdinalIgnoreCase)));
    }
}
