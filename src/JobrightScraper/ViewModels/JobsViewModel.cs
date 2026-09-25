using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobrightScraper.Models;
using JobrightScraper.Services;

namespace JobrightScraper.ViewModels;

public partial class JobsViewModel : ObservableObject
{
    private readonly ICookieBridge _cookieBridge;
    private readonly IJobScraper _scraper;
    private readonly JobExportService _exportService;
    private readonly FileDialogService _fileDialog;
    private readonly Action _showBrowserTab;
    private CancellationTokenSource? _scrapeCts;

    [ObservableProperty]
    private string _keywords = string.Empty;

    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private WorkMode _workMode = WorkMode.All;

    [ObservableProperty]
    private int _minMatchScore;

    [ObservableProperty]
    private int _maxJobs = 50;

    [ObservableProperty]
    private string _status = "Ready. Log in on the Browser tab, then start a scrape.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScrapeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isScraping;

    public JobsViewModel(
        ICookieBridge cookieBridge,
        IJobScraper scraper,
        JobExportService exportService,
        FileDialogService fileDialog,
        Action showBrowserTab)
    {
        _cookieBridge = cookieBridge;
        _scraper = scraper;
        _exportService = exportService;
        _fileDialog = fileDialog;
        _showBrowserTab = showBrowserTab;
        Jobs.CollectionChanged += (_, _) =>
        {
            ExportCsvCommand.NotifyCanExecuteChanged();
            ExportExcelCommand.NotifyCanExecuteChanged();
        };
    }

    public ObservableCollection<JobListing> Jobs { get; } = [];

    public IReadOnlyList<WorkMode> WorkModes { get; } = Enum.GetValues<WorkMode>();

    [RelayCommand(CanExecute = nameof(CanScrape))]
    private async Task ScrapeAsync()
    {
        if (!_cookieBridge.IsReady)
        {
            Status = "Open the Browser tab and wait for WebView2 to finish starting.";
            _showBrowserTab();
            return;
        }

        IReadOnlyList<BrowserCookie> cookies;
        try
        {
            cookies = await _cookieBridge.GetJobrightCookiesAsync();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            return;
        }

        if (!SessionCookieHelper.LooksLoggedIn(cookies))
        {
            Status = "Log in on the Browser tab first, then click I'm logged in.";
            _showBrowserTab();
            return;
        }

        _scrapeCts?.Dispose();
        _scrapeCts = new CancellationTokenSource();
        IsScraping = true;
        Jobs.Clear();
        Status = "Scraping…";

        var filters = new SearchFilters
        {
            Keywords = Keywords.Trim(),
            Location = Location.Trim(),
            WorkMode = WorkMode,
            MinMatchScore = Math.Clamp(MinMatchScore, 0, 100),
            MaxJobs = Math.Clamp(MaxJobs, 1, 200)
        };

        var progress = new Progress<ScrapeProgress>(update =>
        {
            Status = $"{update.Message} ({update.Current}/{update.Total})";
        });
        var jobFound = new Progress<JobListing>(job =>
        {
            if (!Jobs.Any(existing => string.Equals(existing.Url, job.Url, StringComparison.OrdinalIgnoreCase)))
            {
                Jobs.Add(job);
            }
        });

        try
        {
            await _scraper.ScrapeAsync(cookies, filters, progress, jobFound, _scrapeCts.Token);
            Status = $"Finished. {Jobs.Count} job(s) scraped.";
        }
        catch (OperationCanceledException)
        {
            Status = $"Stopped. {Jobs.Count} job(s) kept.";
        }
        catch (Exception ex)
        {
            Status = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsScraping = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _scrapeCts?.Cancel();
        Status = "Stopping…";
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportCsvAsync()
    {
        var path = _fileDialog.PickSavePath(
            "CSV files (*.csv)|*.csv",
            $"jobright-jobs-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            ".csv");
        if (path is null)
        {
            return;
        }

        try
        {
            await _exportService.ExportCsvAsync(Jobs, path, CancellationToken.None);
            Status = $"Saved CSV to {path}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportExcelAsync()
    {
        var path = _fileDialog.PickSavePath(
            "Excel files (*.xlsx)|*.xlsx",
            $"jobright-jobs-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
            ".xlsx");
        if (path is null)
        {
            return;
        }

        try
        {
            await _exportService.ExportExcelAsync(Jobs, path, CancellationToken.None);
            Status = $"Saved Excel to {path}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private bool CanScrape() => !IsScraping;

    private bool CanStop() => IsScraping;

    private bool CanExport() => !IsScraping && Jobs.Count > 0;
}
