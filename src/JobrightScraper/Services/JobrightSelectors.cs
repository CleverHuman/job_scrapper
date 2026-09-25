namespace JobrightScraper.Services;

/// <summary>
/// Jobright is a Next.js app and restyles often. Keep every selector here so a
/// site change only requires edits in one file.
/// </summary>
internal static class JobrightSelectors
{
    public const string SearchInput =
        "input[placeholder*='Search' i], input[placeholder*='keyword' i], input[placeholder*='job title' i], input[type='search']";

    public const string LocationInput =
        "input[placeholder*='Location' i], input[placeholder*='City' i], input[placeholder*='Where' i]";

    public const string JobInfoLink = "a[href*='/jobs/info/']";

    public static string WorkModeControl(string label) =>
        $"button:has-text('{label}'), [role='button']:has-text('{label}'), label:has-text('{label}'), span:has-text('{label}')";

    public const string CollectJobsScript = """
        () => {
          const absolute = (href) => {
            try { return new URL(href, location.origin).href; }
            catch { return href; }
          };

          const textOf = (el) => (el?.innerText || el?.textContent || '').replace(/\s+/g, ' ').trim();

          const scoreFrom = (text) => {
            const match = text.match(/(\d{1,3})\s*%/);
            if (!match) return null;
            const value = Number(match[1]);
            return Number.isFinite(value) ? value : null;
          };

          const workModeFrom = (text) => {
            if (/\bremote\b/i.test(text)) return 'Remote';
            if (/\bhybrid\b/i.test(text)) return 'Hybrid';
            if (/\bon[\s-]?site\b/i.test(text) || /\bin[\s-]?office\b/i.test(text)) return 'Onsite';
            return '';
          };

          const links = [...document.querySelectorAll("a[href*='/jobs/info/']")];
          const seen = new Set();
          const jobs = [];

          for (const link of links) {
            const url = absolute(link.getAttribute('href') || '');
            if (!url || seen.has(url)) continue;
            seen.add(url);

            const card = link.closest("article, [class*='card' i], [class*='job' i], li, [role='listitem']") || link;
            const text = textOf(card);
            const title = textOf(link) || text.split('·')[0] || '';
            const company = textOf(card.querySelector("[class*='company' i], [data-testid*='company' i]")) || '';

            jobs.push({
              title,
              company,
              location: textOf(card.querySelector("[class*='location' i], [data-testid*='location' i]")) || '',
              workMode: workModeFrom(text),
              matchScore: scoreFrom(text),
              salary: (text.match(/\$[\d,]+(?:\s*[-–]\s*\$[\d,]+)?(?:\s*[kK])?(?:\s*\/\s*(?:yr|year|mo|hour|hr))?/) || [''])[0],
              postedAt: (text.match(/\b(\d+\s*(?:minute|hour|day|week|month)s?\s+ago|today|yesterday)\b/i) || [''])[0],
              url
            });
          }

          return jobs;
        }
        """;

    public const string ScrollFeedScript = """
        () => {
          const candidates = [
            ...document.querySelectorAll("[class*='list' i], [class*='feed' i], [class*='scroll' i], [role='feed'], [role='list'], aside, main")
          ];

          const scroller = candidates.find(el => el.scrollHeight - el.clientHeight > 80) || document.scrollingElement;
          if (!scroller) return { before: 0, after: 0, height: 0 };

          const before = scroller.scrollTop;
          scroller.scrollBy(0, Math.max(scroller.clientHeight, 600));
          return {
            before,
            after: scroller.scrollTop,
            height: scroller.scrollHeight
          };
        }
        """;

    public const string ExtractDetailScript = """
        () => {
          const textOf = (el) => (el?.innerText || el?.textContent || '').replace(/\s+/g, ' ').trim();
          const allText = textOf(document.body);

          const heading = document.querySelector("h1, [class*='job-title' i], [data-testid*='title' i]");
          const company = document.querySelector("[class*='company' i], [data-testid*='company' i], a[href*='/company']");
          const description = document.querySelector("[class*='description' i], [data-testid*='description' i], article, [class*='content' i]");

          const sectionAfter = (labels) => {
            const nodes = [...document.querySelectorAll("h1,h2,h3,h4,strong,span,div")];
            for (const node of nodes) {
              const label = textOf(node);
              if (!labels.some(l => label.toLowerCase() === l || label.toLowerCase().startsWith(l))) continue;
              const parent = node.parentElement;
              const sibling = node.nextElementSibling;
              const chunk = textOf(sibling) || textOf(parent);
              if (chunk && chunk.length > label.length + 8) return chunk;
            }
            return '';
          };

          const scoreMatch = allText.match(/(\d{1,3})\s*%/);
          const salaryMatch = allText.match(/\$[\d,]+(?:\s*[-–]\s*\$[\d,]+)?(?:\s*[kK])?(?:\s*\/\s*(?:yr|year|mo|hour|hr))?/);
          const postedMatch = allText.match(/\b(\d+\s*(?:minute|hour|day|week|month)s?\s+ago|today|yesterday)\b/i);
          const workMode = /\bremote\b/i.test(allText) ? 'Remote'
            : /\bhybrid\b/i.test(allText) ? 'Hybrid'
            : (/\bon[\s-]?site\b/i.test(allText) || /\bin[\s-]?office\b/i.test(allText)) ? 'Onsite'
            : '';

          return {
            title: textOf(heading),
            company: textOf(company),
            location: textOf(document.querySelector("[class*='location' i], [data-testid*='location' i]")),
            workMode,
            matchScore: scoreMatch ? Number(scoreMatch[1]) : null,
            salary: salaryMatch ? salaryMatch[0] : '',
            postedAt: postedMatch ? postedMatch[0] : '',
            description: textOf(description),
            requirements: sectionAfter(['requirements', 'qualifications', 'what you', 'you will need']),
            skills: sectionAfter(['skills', 'skill match', 'required skills'])
          };
        }
        """;
}
