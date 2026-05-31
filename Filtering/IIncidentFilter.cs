using BoeingIncidentWatcher.Models;

namespace BoeingIncidentWatcher.Filtering;

internal interface IIncidentFilter
{
    NewsItem? Apply(NewsItem item);
}
