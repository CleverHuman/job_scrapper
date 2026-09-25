# Jobright Scraper

Windows desktop app that logs into [Jobright.ai](https://jobright.ai) in an embedded browser, scrapes recommended jobs with your session, and exports them to CSV or Excel.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- WebView2 Runtime (already installed on current Windows 10/11)
- Playwright Chromium (installed once after the first build)

## Build

```powershell
dotnet restore JobrightScraper.sln
dotnet build JobrightScraper.sln
```

Install Playwright's Chromium browser once:

```powershell
powershell -ExecutionPolicy Bypass -File src/JobrightScraper/bin/Debug/net8.0-windows/playwright.ps1 install chromium
```

Run:

```powershell
dotnet run --project src/JobrightScraper/JobrightScraper.csproj
```

## How to use

1. Open the **Browser** tab and sign in to Jobright. Complete any CAPTCHA there.
2. Click **I'm logged in** when the recommend feed is visible.
3. Switch to **Jobs**, set keywords, location, work mode, min match score, and max jobs.
4. Click **Scrape**. The app copies your WebView2 cookies into Playwright and reads the feed.
5. Export with **Export CSV** or **Export Excel**.

The scraper uses your own logged-in session. It does not bypass login. Jobright can change its page layout; selectors live in `src/JobrightScraper/Services/JobrightSelectors.cs`.
