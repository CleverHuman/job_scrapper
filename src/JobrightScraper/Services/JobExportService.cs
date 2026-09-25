using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using CsvHelper;
using JobrightScraper.Models;

namespace JobrightScraper.Services;

public sealed class JobExportService
{
    public async Task ExportCsvAsync(IEnumerable<JobListing> jobs, string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(jobs.Select(ToExportRow), cancellationToken);
    }

    public Task ExportExcelAsync(IEnumerable<JobListing> jobs, string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Jobs");
        var rows = jobs.Select(ToExportRow).ToList();

        worksheet.Cell(1, 1).InsertTable(rows, "Jobs", true);
        worksheet.Columns().AdjustToContents(1, 40);
        workbook.SaveAs(path);
        return Task.CompletedTask;
    }

    private static JobExportRow ToExportRow(JobListing job) => new()
    {
        Title = job.Title,
        Company = job.Company,
        Location = job.Location,
        WorkMode = job.WorkMode,
        JobType = job.JobType,
        Salary = job.Salary,
        PostedAt = job.PostedAt,
        ApplyUrl = job.Url,
        Description = job.Description,
        Requirements = job.Requirements,
        Skills = job.Skills,
        ScrapedAt = job.ScrapedAt.ToString("u", CultureInfo.InvariantCulture)
    };

    private sealed class JobExportRow
    {
        public string Title { get; init; } = string.Empty;
        public string Company { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string WorkMode { get; init; } = string.Empty;
        public string JobType { get; init; } = string.Empty;
        public string Salary { get; init; } = string.Empty;
        public string PostedAt { get; init; } = string.Empty;
        public string ApplyUrl { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Requirements { get; init; } = string.Empty;
        public string Skills { get; init; } = string.Empty;
        public string ScrapedAt { get; init; } = string.Empty;
    }
}
