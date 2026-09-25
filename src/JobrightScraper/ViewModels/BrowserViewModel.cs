using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobrightScraper.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace JobrightScraper.ViewModels;

public partial class BrowserViewModel : ObservableObject
{
    private readonly WebViewCookieBridge _cookieBridge;
    private WebView2? _webView;

    [ObservableProperty]
    private string _status = "Starting browser…";

    [ObservableProperty]
    private bool _hasSession;

    [ObservableProperty]
    private string _currentUrl = AppPaths.RecommendUrl;

    public BrowserViewModel(WebViewCookieBridge cookieBridge)
    {
        _cookieBridge = cookieBridge;
    }

    public async Task AttachWebViewAsync(WebView2 webView)
    {
        _webView = webView;
        Directory.CreateDirectory(AppPaths.WebView2UserData);

        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebView2UserData);
        await webView.EnsureCoreWebView2Async(environment);

        _cookieBridge.Attach(webView.CoreWebView2);
        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        webView.CoreWebView2.NavigationCompleted += async (_, _) => await RefreshSessionAsync();
        webView.CoreWebView2.SourceChanged += (_, _) => CurrentUrl = webView.CoreWebView2.Source;

        Status = "Browser ready. Sign in to Jobright if needed.";
        NavigateToRecommend();
        await RefreshSessionAsync();
    }

    [RelayCommand]
    private void OpenJobright() => NavigateToRecommend();

    [RelayCommand]
    private async Task ConfirmLoginAsync()
    {
        await RefreshSessionAsync();
        Status = HasSession
            ? "Session cookies found. You can scrape from the Jobs tab."
            : "No session cookies yet. Finish signing in on this page.";
    }

    public async Task RefreshSessionAsync()
    {
        if (!_cookieBridge.IsReady)
        {
            HasSession = false;
            Status = "WebView2 is still starting…";
            return;
        }

        try
        {
            var cookies = await _cookieBridge.GetJobrightCookiesAsync();
            HasSession = SessionCookieHelper.LooksLoggedIn(cookies);
            Status = HasSession
                ? $"Logged in ({cookies.Count} Jobright cookies)."
                : cookies.Count == 0
                    ? "No Jobright cookies yet. Sign in inside this browser."
                    : $"Found {cookies.Count} cookies. Click I'm logged in after you finish sign-in.";
        }
        catch (Exception ex)
        {
            HasSession = false;
            Status = ex.Message;
        }
    }

    private void NavigateToRecommend()
    {
        if (_webView?.CoreWebView2 is null)
        {
            return;
        }

        _webView.CoreWebView2.Navigate(AppPaths.RecommendUrl);
        CurrentUrl = AppPaths.RecommendUrl;
    }
}
