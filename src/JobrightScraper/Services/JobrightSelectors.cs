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

    public static string WorkModeControl(string label) =>
        $"button:has-text('{label}'), [role='button']:has-text('{label}'), label:has-text('{label}'), span:has-text('{label}')";

    public const string CollectJobsScript = """
        (() => {
          const absolute = (href) => {
            try { return new URL(href, location.origin).href; }
            catch { return href || ''; }
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

          const links = [...document.querySelectorAll("a[href*='/jobs/info/'], a[href*='/jobs/']")];
          const seen = new Set();
          const jobs = [];

          const pushJob = (url, card, titleHint) => {
            if (!url || seen.has(url)) return;
            seen.add(url);
            const text = textOf(card);
            jobs.push({
              title: titleHint || text.split('·')[0] || '',
              company: textOf(card.querySelector("[class*='company' i], [data-testid*='company' i]")) || '',
              location: textOf(card.querySelector("[class*='location' i], [data-testid*='location' i]")) || '',
              workMode: workModeFrom(text),
              matchScore: scoreFrom(text),
              salary: (text.match(/\$[\d,]+(?:\s*[-–]\s*\$[\d,]+)?(?:\s*[kK])?(?:\s*\/\s*(?:yr|year|mo|hour|hr))?/) || [''])[0],
              postedAt: (text.match(/\b(\d+\s*(?:minute|hour|day|week|month)s?\s+ago|today|yesterday)\b/i) || [''])[0],
              url
            });
          };

          for (const link of links) {
            const url = absolute(link.getAttribute('href') || '');
            if (!/\/jobs\/info\//i.test(url) && !/\/jobs\//i.test(url)) continue;
            if (/\/jobs\/(recommend|search|resume|profile)\b/i.test(url)) continue;
            const card = link.closest("article, [class*='card' i], [class*='job' i], li, [role='listitem']") || link;
            pushJob(url, card, textOf(link));
          }

          for (const node of document.querySelectorAll("[data-job-id], [data-jobid], [data-id*='job' i]")) {
            const id = node.getAttribute('data-job-id') || node.getAttribute('data-jobid') || node.getAttribute('data-id');
            if (!id) continue;
            pushJob(`${location.origin}/jobs/info/${id}`, node, textOf(node.querySelector("h2,h3,a,[class*='title' i]")));
          }

          return jobs;
        })()
        """;

    public const string ScrollFeedScript = """
        (() => {
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
        })()
        """;
}
