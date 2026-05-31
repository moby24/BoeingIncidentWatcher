namespace BoeingIncidentWatcher.Configuration;

internal sealed class Settings
{
    public int PollIntervalMinutes { get; private set; }
    public string GoogleQuery { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string DashboardUrl { get; private set; } = string.Empty;
    public int MaxNewsItems { get; private set; }
    public bool RequireIncidentIdentifier { get; private set; }
    public bool EnableAviationHeraldGoogleNews { get; private set; }
    public string AviationHeraldGoogleQuery { get; private set; } = string.Empty;
    public bool EnableAviationHeraldImport { get; private set; }
    public string AviationHeraldImportPath { get; private set; } = string.Empty;
    public string StateFile { get; private set; } = string.Empty;

    public static Settings Load(string path)
    {
        var pairs = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("#"))
            .Select(ParseLine)
            .Where(pair => pair.Key != null)
            .ToDictionary(pair => pair.Key!, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return new Settings
        {
            PollIntervalMinutes = ParseInt(pairs, "PollIntervalMinutes", 5),
            GoogleQuery = GetOrDefault(pairs, "GoogleQuery", "boeing incident"),
            Region = GetOrDefault(pairs, "Region", "en-US"),
            Country = GetOrDefault(pairs, "Country", "US"),
            DashboardUrl = GetOrDefault(pairs, "DashboardUrl", "http://localhost:5055/"),
            MaxNewsItems = ParseInt(pairs, "MaxNewsItems", 40),
            RequireIncidentIdentifier = ParseBool(pairs, "RequireIncidentIdentifier", true),
            EnableAviationHeraldGoogleNews = ParseBool(pairs, "EnableAviationHeraldGoogleNews", true),
            AviationHeraldGoogleQuery = GetOrDefault(pairs, "AviationHeraldGoogleQuery", "site:avherald.com (Boeing OR B738 OR B39M OR B789 OR B788 OR B77W OR B763 OR B752) (incident OR accident OR crash OR emergency OR runway OR engine)"),
            EnableAviationHeraldImport = ParseBool(pairs, "EnableAviationHeraldImport", false),
            AviationHeraldImportPath = GetOrDefault(pairs, "AviationHeraldImportPath", "Data/aviation-herald-import"),
            StateFile = GetOrDefault(pairs, "StateFile", "Data/news.tsv")
        };
    }

    private static KeyValuePair<string?, string> ParseLine(string line)
    {
        var index = line.IndexOf('=');
        if (index <= 0 || index == line.Length - 1)
        {
            return new KeyValuePair<string?, string>(null, string.Empty);
        }

        var key = line.Substring(0, index).Trim();
        var value = line.Substring(index + 1).Trim();
        return new KeyValuePair<string?, string>(key, value);
    }

    private static string GetOrDefault(IDictionary<string, string> pairs, string key, string defaultValue)
    {
        return pairs.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : defaultValue;
    }

    private static int ParseInt(IDictionary<string, string> pairs, string key, int defaultValue)
    {
        return pairs.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static bool ParseBool(IDictionary<string, string> pairs, string key, bool defaultValue)
    {
        return pairs.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
