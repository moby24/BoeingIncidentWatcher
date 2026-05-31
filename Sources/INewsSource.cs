using BoeingIncidentWatcher.Models;

namespace BoeingIncidentWatcher.Sources;

internal interface INewsSource
{
    string Name { get; }
    Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken cancellationToken);
}
