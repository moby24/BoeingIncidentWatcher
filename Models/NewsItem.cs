namespace BoeingIncidentWatcher.Models;

internal sealed class NewsItem
{
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string MatchReason { get; set; } = string.Empty;
    public DateTimeOffset Published { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    public NewsItem Clone()
    {
        return new NewsItem
        {
            Title = Title,
            Link = Link,
            Description = Description,
            SourceName = SourceName,
            Identifier = Identifier,
            MatchReason = MatchReason,
            Published = Published,
            AddedAt = AddedAt
        };
    }
}
