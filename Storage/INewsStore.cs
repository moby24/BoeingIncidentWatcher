using BoeingIncidentWatcher.Models;

namespace BoeingIncidentWatcher.Storage;

internal interface INewsStore
{
    int Count { get; }
    DateTimeOffset LastRefresh { get; }
    string LastError { get; }
    int Merge(IReadOnlyList<NewsItem> incoming);
    void SetLastError(string error);
    List<NewsItem> GetItems();
}
