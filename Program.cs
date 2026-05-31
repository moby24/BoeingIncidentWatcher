using BoeingIncidentWatcher.Configuration;
using BoeingIncidentWatcher.Dashboard;
using BoeingIncidentWatcher.Filtering;
using BoeingIncidentWatcher.Services;
using BoeingIncidentWatcher.Sources;
using BoeingIncidentWatcher.Storage;

namespace BoeingIncidentWatcher;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var configPath = args.Length > 0 ? args[0] : "settings.ini";

        if (!File.Exists(configPath))
        {
            Console.WriteLine("settings.ini not found. Create it from settings.example.ini.");
            return 1;
        }

        var settings = Settings.Load(configPath);
        var incidentFilter = new IncidentFilter(settings.RequireIncidentIdentifier);
        var newsStore = new TsvNewsStore(settings.StateFile, settings.MaxNewsItems, incidentFilter);
        var sources = new List<INewsSource>
        {
            new GoogleNewsRssSource(settings.GoogleQuery, settings.Region, settings.Country)
        };

        if (settings.EnableAviationHeraldGoogleNews)
        {
            sources.Add(new GoogleNewsRssSource(
                settings.AviationHeraldGoogleQuery,
                settings.Region,
                settings.Country,
                "Aviation Herald via Google News"));
        }

        if (settings.EnableAviationHeraldImport)
        {
            sources.Add(new AviationHeraldImportSource(settings.AviationHeraldImportPath));
        }

        var pollingService = new NewsPollingService(settings, sources, incidentFilter, newsStore);
        var dashboard = new DashboardServer(settings.DashboardUrl, newsStore, settings);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine("Boeing Incident Dashboard started.");
        Console.WriteLine("Open: " + settings.DashboardUrl);
        Console.WriteLine("Press Ctrl+C to stop.");

        await Task.WhenAll(
            pollingService.RunAsync(cts.Token),
            dashboard.RunAsync(cts.Token));

        return 0;
    }
}
