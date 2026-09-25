using System.Text.Json;
using JobrightScraper.Models;

namespace JobrightScraper.Services;

internal static class JobrightJson
{
    public static IReadOnlyList<JobListing> ParseRecommendResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("success", out var success)
            && success.ValueKind is JsonValueKind.False)
        {
            var message = ReadString(root, "message") ?? "Jobright API returned success=false.";
            throw new InvalidOperationException(message);
        }

        var jobs = new List<JobListing>();
        foreach (var item in EnumerateJobItems(root))
        {
            var listing = ToListing(item);
            if (!string.IsNullOrWhiteSpace(listing.Url) || !string.IsNullOrWhiteSpace(listing.Title))
            {
                jobs.Add(listing);
            }
        }

        return jobs;
    }

    private static IEnumerable<JsonElement> EnumerateJobItems(JsonElement root)
    {
        if (TryGetProperty(root, out var result, "result")
            && TryGetProperty(result, out var jobList, "jobList", "jobs")
            && jobList.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in jobList.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static JobListing ToListing(JsonElement item)
    {
        var job = TryGetProperty(item, out var jobResult, "jobResult") ? jobResult : item;
        var company = TryGetProperty(item, out var companyResult, "companyResult") ? companyResult : item;
        var jobId = ReadString(job, "jobId", "id") ?? string.Empty;
        var applyUrl = FindApplyUrl(job)
            ?? FindApplyUrl(item)
            ?? ReadString(job, "applyLink", "applyUrl", "applyURL", "jobApplyLink", "originalApplyLink", "externalApplyLink");

        var requirements = ReadList(job, "requirements");
        var skills = ReadList(job, "skillSummaries", "skills");
        var responsibilities = ReadList(job, "coreResponsibilities");
        var summary = ReadString(job, "jobSummary", "jobDescription") ?? string.Empty;

        var descriptionParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary))
        {
            descriptionParts.Add(summary);
        }

        if (responsibilities.Count > 0)
        {
            descriptionParts.Add("Responsibilities:\n" + string.Join("\n", responsibilities.Select(item => "- " + item)));
        }

        return new JobListing
        {
            Title = ReadString(job, "jobTitle", "title") ?? string.Empty,
            Company = ReadString(company, "companyName", "name") ?? string.Empty,
            Location = ReadString(job, "jobLocation", "location") ?? string.Empty,
            WorkMode = InferRemote(
                NormalizeWorkMode(ReadString(job, "workModel", "workMode")),
                ReadString(job, "jobLocation", "location"),
                ReadString(job, "jobTitle", "title")),
            JobType = JobRules.NormalizeJobType(ReadString(job, "jobType", "employmentType", "workType", "jobEmploymentType", "positionType", "empType")),
            Salary = ReadString(job, "salaryDesc", "salary") ?? string.Empty,
            PostedAt = ReadString(job, "publishTimeDesc", "postedAt") ?? string.Empty,
            PostedAtUtc = PostedTimeParser.Parse(
                ReadString(job, "publishTimeDesc", "postedAt"),
                ReadInt64(job, "publishTime", "publishedAt", "createTime", "createdAt")
                ?? ReadInt64(item, "publishTime", "publishedAt")),
            JobId = jobId,
            Url = ApplyUrlResolver.IsCompanyUrl(applyUrl) ? applyUrl! : applyUrl ?? string.Empty,
            Description = string.Join("\n\n", descriptionParts),
            Requirements = string.Join("\n", requirements.Select(item => "- " + item)),
            Skills = string.Join(", ", skills),
            ScrapedAt = DateTime.UtcNow
        };
    }

    private static string? FindApplyUrl(JsonElement element) => ApplyUrlResolver.FindApplyUrl(element);

    public static bool Matches(JobListing job, SearchFilters filters)
    {
        if (filters.WorkMode is not WorkMode.All
            && !job.WorkMode.Equals(filters.WorkMode.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!PostedTimeParser.IsWithinHours(job.PostedAtUtc, filters.MaxAgeHours))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filters.Keywords))
        {
            var haystack = $"{job.Title} {job.Company} {job.Description} {job.Requirements} {job.Skills}";
            if (!ContainsAllTokens(haystack, filters.Keywords))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(filters.Location)
            && !ContainsAllTokens($"{job.Location} {job.WorkMode}", filters.Location))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsAllTokens(string haystack, string query)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.All(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeWorkMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Contains("remote", StringComparison.OrdinalIgnoreCase))
        {
            return "Remote";
        }

        if (value.Contains("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return "Hybrid";
        }

        if (value.Contains("site", StringComparison.OrdinalIgnoreCase) || value.Contains("office", StringComparison.OrdinalIgnoreCase))
        {
            return "Onsite";
        }

        return value.Trim();
    }

    private static string InferRemote(string workMode, string? location, string? title)
    {
        if (!string.IsNullOrWhiteSpace(workMode))
        {
            return workMode;
        }

        var haystack = $"{location} {title}";
        return haystack.Contains("remote", StringComparison.OrdinalIgnoreCase) ? "Remote" : workMode;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.ToString()
        };
    }

    private static int? ReadInt(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var value, names))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static long? ReadInt64(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var value, names))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static List<string> ReadList(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var value, names))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToList();
        }

        if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return [value.GetString()!];
        }

        return [];
    }
}
