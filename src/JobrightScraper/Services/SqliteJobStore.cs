using System.Globalization;
using System.IO;
using JobrightScraper.Models;
using Microsoft.Data.Sqlite;

namespace JobrightScraper.Services;

public sealed class SqliteJobStore : IJobStore, IDisposable
{
    private readonly object _gate = new();
    private readonly string _connectionString;

    public SqliteJobStore(string? databasePath = null)
    {
        var path = databasePath ?? AppPaths.Database;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Path.Equals(Path.GetPathRoot(directory), directory))
        {
            Directory.CreateDirectory(directory);
        }
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        EnsureSchema();
    }

    public bool Exists(string? jobId, string? url)
    {
        lock (_gate)
        {
            using var connection = Open();
            if (!string.IsNullOrWhiteSpace(jobId))
            {
                using var byId = connection.CreateCommand();
                byId.CommandText = "SELECT 1 FROM Jobs WHERE JobId = $id LIMIT 1";
                byId.Parameters.AddWithValue("$id", jobId);
                if (byId.ExecuteScalar() is not null)
                {
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(url))
            {
                using var byUrl = connection.CreateCommand();
                byUrl.CommandText = "SELECT 1 FROM Jobs WHERE Url = $url LIMIT 1";
                byUrl.Parameters.AddWithValue("$url", url);
                return byUrl.ExecuteScalar() is not null;
            }

            return false;
        }
    }

    public void Upsert(JobListing job)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Jobs (
                    JobId, Title, Company, Location, WorkMode, JobType, Salary,
                    PostedAt, PostedAtUtc, Url, Description, Requirements, Skills, ScrapedAt)
                VALUES (
                    $jobId, $title, $company, $location, $workMode, $jobType, $salary,
                    $postedAt, $postedAtUtc, $url, $description, $requirements, $skills, $scrapedAt)
                ON CONFLICT(Url) DO UPDATE SET
                    JobId = excluded.JobId,
                    Title = excluded.Title,
                    Company = excluded.Company,
                    Location = excluded.Location,
                    WorkMode = excluded.WorkMode,
                    JobType = excluded.JobType,
                    Salary = excluded.Salary,
                    PostedAt = excluded.PostedAt,
                    PostedAtUtc = excluded.PostedAtUtc,
                    Description = excluded.Description,
                    Requirements = excluded.Requirements,
                    Skills = excluded.Skills,
                    ScrapedAt = excluded.ScrapedAt
                """;
            command.Parameters.AddWithValue("$jobId", job.JobId);
            command.Parameters.AddWithValue("$title", job.Title);
            command.Parameters.AddWithValue("$company", job.Company);
            command.Parameters.AddWithValue("$location", job.Location);
            command.Parameters.AddWithValue("$workMode", job.WorkMode);
            command.Parameters.AddWithValue("$jobType", job.JobType);
            command.Parameters.AddWithValue("$salary", job.Salary);
            command.Parameters.AddWithValue("$postedAt", job.PostedAt);
            command.Parameters.AddWithValue(
                "$postedAtUtc",
                job.PostedAtUtc?.ToString("o", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$url", string.IsNullOrWhiteSpace(job.Url) ? $"jobright:{job.JobId}" : job.Url);
            command.Parameters.AddWithValue("$description", job.Description);
            command.Parameters.AddWithValue("$requirements", job.Requirements);
            command.Parameters.AddWithValue("$skills", job.Skills);
            command.Parameters.AddWithValue("$scrapedAt", job.ScrapedAt.ToString("o", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureSchema()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Jobs (
                    Url TEXT PRIMARY KEY,
                    JobId TEXT,
                    Title TEXT,
                    Company TEXT,
                    Location TEXT,
                    WorkMode TEXT,
                    JobType TEXT,
                    Salary TEXT,
                    PostedAt TEXT,
                    PostedAtUtc TEXT,
                    Description TEXT,
                    Requirements TEXT,
                    Skills TEXT,
                    ScrapedAt TEXT
                );
                CREATE INDEX IF NOT EXISTS IX_Jobs_JobId ON Jobs(JobId);
                """;
            command.ExecuteNonQuery();
        }
    }
}
