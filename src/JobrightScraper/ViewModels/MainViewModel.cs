using CommunityToolkit.Mvvm.ComponentModel;
using JobrightScraper.Services;

namespace JobrightScraper.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        var cookieBridge = new WebViewCookieBridge();
        var store = new SqliteJobStore();
        Browser = new BrowserViewModel(cookieBridge);
        Jobs = new JobsViewModel(
            cookieBridge,
            new JobScraperService(store),
            new JobExportService(),
            new FileDialogService(),
            () => SelectedTabIndex = 0);
    }

    public BrowserViewModel Browser { get; }

    public JobsViewModel Jobs { get; }

    [ObservableProperty]
    private int _selectedTabIndex;
}
