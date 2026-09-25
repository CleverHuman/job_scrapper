using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobrightScraper.Models;
using JobrightScraper.Services;

namespace JobrightScraper.ViewModels;

public partial class JobsViewModel : ObservableObject
{
    private readonly IBrowserSession _browser;
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
    private int _maxJobs = 50;

    [ObservableProperty]
    private string _status = "Ready. Log in on the Browser tab, then start a scrape.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScrapeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isScraping;

    public JobsViewModel(
        IBrowserSession browser,
        IJobScraper scraper,
        JobExportService exportService,
        FileDialogService fileDialog,
        Action showBrowserTab)
    {
        _browser = browser;
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

    [RelayCommand(CanExecute = nameof(CanScrape))]
    private async Task ScrapeAsync()
    {
        if (!_browser.IsReady)
        {
            Status = "Open the Browser tab and wait for WebView2 to finish starting.";
            _showBrowserTab();
            return;
        }

        IReadOnlyList<BrowserCookie> cookies;
        try
        {
            cookies = await _browser.GetJobrightCookiesAsync();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            return;
        }

        var onRecommendFeed = _browser.CurrentUrl?.Contains("/jobs/", StringComparison.OrdinalIgnoreCase) == true;
        if (!SessionCookieHelper.LooksLoggedIn(cookies) && !onRecommendFeed)
        {
            Status = "Log in on the Browser tab first, then click I'm logged in.";
            _showBrowserTab();
            return;
        }

        _scrapeCts?.Dispose();
        _scrapeCts = new CancellationTokenSource();
        IsScraping = true;
        Jobs.Clear();
        Status = "Scraping remote jobs from the last 24 hours…";

        var filters = new SearchFilters
        {
            Keywords = Keywords.Trim(),
            Location = Location.Trim(),
            WorkMode = WorkMode.Remote,
            MaxJobs = Math.Clamp(MaxJobs, 1, 200),
            MaxAgeHours = 24
        };

        var progress = new Progress<ScrapeProgress>(update =>
        {
            Status = $"{update.Message} ({update.Current}/{update.Total})";
        });
        var jobFound = new Progress<JobListing>(InsertSorted);

        try
        {
            await _scraper.ScrapeAsync(_browser, filters, progress, jobFound, _scrapeCts.Token);
            Status = $"Finished. {Jobs.Count} new remote job(s) from the last 24 hours saved.";
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

    private void InsertSorted(JobListing job)
    {
        if (Jobs.Any(existing =>
                string.Equals(existing.Url, job.Url, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(job.JobId)
                    && string.Equals(existing.JobId, job.JobId, StringComparison.OrdinalIgnoreCase))))
        {
            return;
        }

        var posted = job.PostedAtUtc ?? DateTime.MinValue;
        var index = 0;
        while (index < Jobs.Count && (Jobs[index].PostedAtUtc ?? DateTime.MinValue) >= posted)
        {
            index++;
        }

        Jobs.Insert(index, job);
    }

    private bool CanScrape() => !IsScraping;

    private bool CanStop() => IsScraping;

    private bool CanExport() => !IsScraping && Jobs.Count > 0;
}
