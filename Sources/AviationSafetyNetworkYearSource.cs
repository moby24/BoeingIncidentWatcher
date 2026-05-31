using System.Net;
using System.Text.RegularExpressions;
using BoeingIncidentWatcher.Models;

namespace BoeingIncidentWatcher.Sources;

internal sealed class AviationSafetyNetworkYearSource : INewsSource
{
    private static readonly HttpClient HttpClient = new();

    private static readonly Regex RowRegex = new(
        "<tr[^>]*>(?<row>.*?)</tr>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CellRegex = new(
        "<t[dh][^>]*>(?<cell>.*?)</t[dh]>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex LinkRegex = new(
        "<a\\s+[^>]*href=[\"'](?<href>[^\"']+)[\"'][^>]*>(?<text>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly string _yearUrl;

    public AviationSafetyNetworkYearSource(string yearUrl)
    {
        _yearUrl = yearUrl;
    }

    public string Name => "Aviation Safety Network";

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Checking " + Name + ": " + DateTimeOffset.Now + " -> " + _yearUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, _yearUrl);
        request.Headers.UserAgent.ParseAdd("BoeingIncidentWatcher/1.0 (+local dashboard)");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var items = ParseHtml(html).ToList();
        Console.WriteLine(Name + ": parsed " + items.Count + " item(s).");
        return items;
    }

    private IEnumerable<NewsItem> ParseHtml(string html)
    {
        var parsedAnyRows = false;
        foreach (Match rowMatch in RowRegex.Matches(html))
        {
            var item = ParseTableRow(rowMatch.Groups["row"].Value);
            if (item != null)
            {
                parsedAnyRows = true;
                yield return item;
            }
        }

        if (parsedAnyRows)
        {
            yield break;
        }

        foreach (var item in ParsePlainTextFallback(html))
        {
            yield return item;
        }
    }

    private NewsItem? ParseTableRow(string rowHtml)
    {
        var cells = CellRegex
            .Matches(rowHtml)
            .Select(match => match.Groups["cell"].Value)
            .ToList();

        if (cells.Count < 7)
        {
            return null;
        }

        var dateText = CleanHtml(cells[0]);
        if (!DateTimeOffset.TryParse(dateText, out var published))
        {
            return null;
        }

        var link = ExtractFirstLink(cells[0]);
        var type = CleanHtml(cells[1]);
        var registration = CleanHtml(cells[2]);
        var operatorName = CleanHtml(cells[3]);
        var fatalities = CleanHtml(cells[4]);
        var location = CleanHtml(cells[5]);
        var damage = CleanHtml(cells[^1]);

        if (string.IsNullOrWhiteSpace(type) || string.Equals(type, "type", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var title = "ASN: " + dateText + " - " + type;
        if (!string.IsNullOrWhiteSpace(registration))
        {
            title += " " + registration;
        }

        if (!string.IsNullOrWhiteSpace(operatorName))
        {
            title += " / " + operatorName;
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            title += " at " + location;
        }

        var description = "accident incident record; type: " + type
            + "; registration: " + registration
            + "; operator: " + operatorName
            + "; fatalities: " + fatalities
            + "; location: " + location
            + "; damage: " + damage;

        return new NewsItem
        {
            Title = title,
            Link = NormalizeUrl(link),
            Description = description,
            SourceName = Name,
            Published = published,
            AddedAt = DateTimeOffset.Now
        };
    }

    private IEnumerable<NewsItem> ParsePlainTextFallback(string html)
    {
        var text = CleanHtml(html);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines.Select(x => Regex.Replace(x, "\\s+", " ").Trim()))
        {
            if (!Regex.IsMatch(line, @"^\d{1,2}\s+[A-Z][a-z]{2}\s+\d{4}\b"))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7)
            {
                continue;
            }

            var dateText = string.Join(' ', parts.Take(3));
            if (!DateTimeOffset.TryParse(dateText, out var published))
            {
                continue;
            }

            yield return new NewsItem
            {
                Title = "ASN: " + line,
                Link = _yearUrl,
                Description = "accident incident record; " + line,
                SourceName = Name,
                Published = published,
                AddedAt = DateTimeOffset.Now
            };
        }
    }

    private static string ExtractFirstLink(string html)
    {
        var match = LinkRegex.Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["href"].Value) : string.Empty;
    }

    private static string CleanHtml(string value)
    {
        var withoutTags = Regex.Replace(value, "<.*?>", " ");
        var normalized = Regex.Replace(withoutTags, "\\s+", " ").Trim();
        return WebUtility.HtmlDecode(normalized);
    }

    private string NormalizeUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return _yearUrl;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var baseUri = new Uri(_yearUrl);
        return new Uri(baseUri, value).ToString();
    }
}
