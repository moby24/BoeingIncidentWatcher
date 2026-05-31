using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BoeingIncidentWatcher.Models;

namespace BoeingIncidentWatcher.Sources;

internal sealed class GoogleNewsRssSource : INewsSource
{
    private static readonly HttpClient HttpClient = new();

    private readonly string _query;
    private readonly string _region;
    private readonly string _country;
    private readonly string _name;

    public GoogleNewsRssSource(string query, string region, string country, string name = "Google News")
    {
        _query = query;
        _region = region;
        _country = country;
        _name = name;
    }

    public string Name => _name;

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken cancellationToken)
    {
        var rssUrl = BuildGoogleNewsRssUrl(_query, _region, _country);
        Console.WriteLine("Checking " + Name + ": " + DateTimeOffset.Now + " -> " + rssUrl);

        var xml = await HttpClient.GetStringAsync(rssUrl, cancellationToken);
        return ParseRss(xml);
    }

    private string BuildGoogleNewsRssUrl(string query, string region, string country)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var safeRegion = string.IsNullOrWhiteSpace(region) ? "en-US" : region;
        var safeCountry = string.IsNullOrWhiteSpace(country) ? "US" : country;
        var languageCode = safeRegion.Contains("-") ? safeRegion.Split('-')[0] : safeRegion;
        return "https://news.google.com/rss/search?q=" + encodedQuery + "&hl=" + safeRegion + "&gl=" + safeCountry + "&ceid=" + safeCountry + ":" + languageCode;
    }

    private List<NewsItem> ParseRss(string xml)
    {
        var result = new List<NewsItem>();
        var doc = XDocument.Parse(xml);

        foreach (var itemNode in doc.Descendants("item"))
        {
            var title = itemNode.Element("title")?.Value?.Trim() ?? "(no title)";
            var link = itemNode.Element("link")?.Value?.Trim() ?? string.Empty;
            var rawDescription = itemNode.Element("description")?.Value ?? string.Empty;
            var publishedRaw = itemNode.Element("pubDate")?.Value;

            if (!DateTimeOffset.TryParse(publishedRaw, out var published))
            {
                published = DateTimeOffset.MinValue;
            }

            result.Add(new NewsItem
            {
                Title = WebUtility.HtmlDecode(title),
                Link = link,
                Description = StripHtml(rawDescription),
                SourceName = Name,
                Published = published,
                AddedAt = DateTimeOffset.Now
            });
        }

        return result;
    }

    private static string StripHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var noTags = Regex.Replace(input, "<.*?>", " ");
        var normalized = Regex.Replace(noTags, "\\s+", " ").Trim();
        return WebUtility.HtmlDecode(normalized);
    }
}
