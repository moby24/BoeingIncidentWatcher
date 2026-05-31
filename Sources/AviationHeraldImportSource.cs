using System.Net;
using System.Text.RegularExpressions;
using BoeingIncidentWatcher.Models;

namespace BoeingIncidentWatcher.Sources;

internal sealed class AviationHeraldImportSource : INewsSource
{
    private static readonly Regex AnchorRegex = new(
        "<a\\s+[^>]*href=[\"'](?<href>[^\"']*\\?article=[^\"']+)[\"'][^>]*>(?<text>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex PlainTextLineRegex = new(
        @"^(?<date>\d{4}-\d{2}-\d{2})\s*\|\s*(?<title>[^|]+)(?:\|\s*(?<url>[^|]+))?(?:\|\s*(?<summary>.+))?$",
        RegexOptions.Compiled);

    private static readonly Regex HeadlineDateRegex = new(
        @"\bon\s+(?<month>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+(?<day>\d{1,2})(?:st|nd|rd|th)?\s+(?<year>\d{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _importPath;

    public AviationHeraldImportSource(string importPath)
    {
        _importPath = importPath;
    }

    public string Name => "Aviation Herald import";

    public Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_importPath))
        {
            Directory.CreateDirectory(_importPath);
            Console.WriteLine(Name + ": import folder created at " + _importPath);
            return Task.FromResult<IReadOnlyList<NewsItem>>(Array.Empty<NewsItem>());
        }

        var items = new List<NewsItem>();
        var files = Directory
            .EnumerateFiles(_importPath)
            .Where(file => file.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file)
            .ToList();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = File.ReadAllText(file);

            if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                items.AddRange(ParseTextImport(content, file));
            }
            else
            {
                items.AddRange(ParseHtmlImport(content, file));
            }
        }

        Console.WriteLine(Name + ": imported " + items.Count + " item(s) from " + files.Count + " file(s).");
        return Task.FromResult<IReadOnlyList<NewsItem>>(items);
    }

    private static IEnumerable<NewsItem> ParseTextImport(string content, string file)
    {
        foreach (var line in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
            {
                continue;
            }

            var match = PlainTextLineRegex.Match(trimmed);
            if (!match.Success)
            {
                continue;
            }

            var title = match.Groups["title"].Value.Trim();
            var url = match.Groups["url"].Success ? match.Groups["url"].Value.Trim() : string.Empty;
            var summary = match.Groups["summary"].Success ? match.Groups["summary"].Value.Trim() : title;

            yield return new NewsItem
            {
                Title = title,
                Link = string.IsNullOrWhiteSpace(url) ? "local-import://" + Path.GetFileName(file) + "#" + Uri.EscapeDataString(title) : NormalizeUrl(url),
                Description = summary,
                SourceName = "Aviation Herald import",
                Published = ParseDate(match.Groups["date"].Value),
                AddedAt = DateTimeOffset.Now
            };
        }
    }

    private static IEnumerable<NewsItem> ParseHtmlImport(string content, string file)
    {
        foreach (Match match in AnchorRegex.Matches(content))
        {
            var title = CleanHtml(match.Groups["text"].Value);
            if (string.IsNullOrWhiteSpace(title) || !LooksLikeAviationHeraldHeadline(title))
            {
                continue;
            }

            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);

            yield return new NewsItem
            {
                Title = title,
                Link = NormalizeUrl(href),
                Description = title,
                SourceName = "Aviation Herald import",
                Published = TryExtractDate(title),
                AddedAt = DateTimeOffset.Now
            };
        }
    }

    private static bool LooksLikeAviationHeraldHeadline(string title)
    {
        return title.StartsWith("Incident:", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("Accident:", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("Crash:", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("Report:", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("Serious incident:", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanHtml(string value)
    {
        var withoutTags = Regex.Replace(value, "<.*?>", " ");
        var normalized = Regex.Replace(withoutTags, "\\s+", " ").Trim();
        return WebUtility.HtmlDecode(normalized);
    }

    private static string NormalizeUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return "https://avherald.com/" + value.TrimStart('/');
    }

    private static DateTimeOffset TryExtractDate(string title)
    {
        var match = HeadlineDateRegex.Match(title);
        if (!match.Success)
        {
            return DateTimeOffset.MinValue;
        }

        var rawDate = match.Groups["month"].Value + " " + match.Groups["day"].Value + " " + match.Groups["year"].Value;
        return DateTimeOffset.TryParse(rawDate, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }

    private static DateTimeOffset ParseDate(string value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }
}
