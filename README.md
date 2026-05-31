# BoeingIncidentWatcher

C# background dashboard that checks Google News every 5 minutes for Boeing incident-related news and shows the results in a local browser dashboard.

## Setup

Install .NET 8 SDK, not only .NET Runtime. Check it with:

```powershell
dotnet --list-sdks
```

You should see a version like `8.0.xxx`.

Edit `settings.ini` if needed:

```ini
PollIntervalMinutes=5
GoogleQuery=boeing incident
Region=en-US
Country=US
DashboardUrl=http://localhost:5055/
MaxNewsItems=40
RequireIncidentIdentifier=true
EnableAviationHeraldGoogleNews=true
AviationHeraldGoogleQuery=site:avherald.com (Boeing OR B738 OR B39M OR B789 OR B788 OR B77W OR B763 OR B752) (incident OR accident OR crash OR emergency OR runway OR engine)
EnableAviationHeraldImport=false
AviationHeraldImportPath=Data/aviation-herald-import
EnableAviationSafetyNetwork=true
AviationSafetyNetworkYearUrl=https://aviation-safety.net/asndb/year/2026
StateFile=Data/news.tsv
```

## Run

```powershell
dotnet run
```

Then open:

```text
http://localhost:5055/
```

## Dashboard

The dashboard shows:

- total stored news items
- latest publish date
- last refresh time
- searchable incident cards with summaries, detected identifiers, and source links

The browser dashboard refreshes every 30 seconds. The background checker polls Google News using `PollIntervalMinutes`.

## Smart incident filter

When `RequireIncidentIdentifier=true`, the dashboard only keeps news that looks like a concrete aviation incident and includes an explicit identifier.

Accepted identifiers include:

- flight numbers, for example `Flight 171`, `AI171`, `UPS 2976`
- aircraft registrations, for example `N704AL`, `VT-ANB`, `G-ZBJI`
- aircraft types, for example `B738`, `B39M`, `B789`, `A320`, `E190`

The item must also include incident wording such as `crash`, `accident`, `incident`, `emergency`, `collision`, `engine failure`, `runway`, `door plug`, `fatal`, `injured`, or similar Ukrainian terms.

## Project structure

The app is split into a source/filter/store/dashboard pipeline:

- `Sources/INewsSource.cs`: common interface for news providers
- `Sources/GoogleNewsRssSource.cs`: Google News RSS provider
- `Sources/AviationHeraldImportSource.cs`: local Aviation Herald import provider
- `Sources/AviationSafetyNetworkYearSource.cs`: Aviation Safety Network yearly database provider
- `Filtering/IncidentFilter.cs`: incident and identifier filter
- `Storage/TsvNewsStore.cs`: local persisted history
- `Dashboard/DashboardServer.cs`: local HTTP dashboard and JSON API
- `Services/NewsPollingService.cs`: polling loop across configured sources

To add another source, such as Aviation Herald, create a new `INewsSource` implementation and register it in `Program.cs`.

## Aviation Herald Automation

The automated Aviation Herald path uses Google News RSS with a restricted query:

```ini
EnableAviationHeraldGoogleNews=true
AviationHeraldGoogleQuery=site:avherald.com (Boeing OR B738 OR B39M OR B789 OR B788 OR B77W OR B763 OR B752) (incident OR accident OR crash OR emergency OR runway OR engine)
```

This checks Google News for indexed Aviation Herald pages every polling cycle. It does not directly scrape `avherald.com`.

## Aviation Safety Network

ASN yearly database polling is enabled with:

```ini
EnableAviationSafetyNetwork=true
AviationSafetyNetworkYearUrl=https://aviation-safety.net/asndb/year/2026
```

The source reads only the configured yearly page and parses the visible table rows into dashboard items. It does not crawl ASN detail pages.

## Aviation Herald Import

Direct scraping of Aviation Herald is intentionally not implemented. Their site blocks some automated access and publishes restrictions around reuse. Use `AviationHeraldImportSource` only with local files you are allowed to use.

To enable local import:

```ini
EnableAviationHeraldImport=true
AviationHeraldImportPath=Data/aviation-herald-import
```

Supported imports:

- saved `.html` / `.htm` pages containing links like `?article=...`
- `.txt` lines in this format:

```text
yyyy-mm-dd | title | url | summary
```

Example:

```text
2026-05-30 | Incident: Example B738 near Kyiv on May 30th 2026, engine shut down | https://avherald.com/h?article=example | Incident: Example B738 near Kyiv on May 30th 2026, engine shut down
```

## Notes

- News history is stored in `Data/news.tsv`.
- The app uses Google News RSS, so it does not need a Google API key.
- If the port is busy, change `DashboardUrl` in `settings.ini`, for example `http://localhost:5056/`.
