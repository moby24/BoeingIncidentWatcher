using BoeingIncidentWatcher.Configuration;
using BoeingIncidentWatcher.Filtering;
using BoeingIncidentWatcher.Models;
using BoeingIncidentWatcher.Sources;
using BoeingIncidentWatcher.Storage;

namespace BoeingIncidentWatcher.Services;

internal sealed class NewsPollingService
{
    private readonly Settings _settings;
    private readonly IReadOnlyList<INewsSource> _sources;
    private readonly IIncidentFilter _incidentFilter;
    private readonly INewsStore _newsStore;

    public NewsPollingService(
        Settings settings,
        IReadOnlyList<INewsSource> sources,
        IIncidentFilter incidentFilter,
        INewsStore newsStore)
    {
        _settings = settings;
        _sources = sources;
        _incidentFilter = incidentFilter;
        _newsStore = newsStore;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshNewsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _newsStore.SetLastError(ex.Message);
                Console.WriteLine("Refresh error: " + ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_settings.PollIntervalMinutes), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task RefreshNewsAsync(CancellationToken cancellationToken)
    {
        var allItems = new List<NewsItem>();

        foreach (var source in _sources)
        {
            var sourceItems = await source.FetchAsync(cancellationToken);
            allItems.AddRange(sourceItems);
        }

        var filteredItems = allItems
            .Where(x => !string.IsNullOrWhiteSpace(x.Link))
            .Select(x => _incidentFilter.Apply(x))
            .Where(x => x != null)
            .OrderByDescending(x => x!.Published)
            .Take(_settings.MaxNewsItems)
            .Select(x => x!)
            .ToList();

        var added = _newsStore.Merge(filteredItems);
        Console.WriteLine("Refresh complete. New items: " + added + ". Total stored: " + _newsStore.Count);
    }
}
