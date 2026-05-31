using System.Text;
using BoeingIncidentWatcher.Filtering;
using BoeingIncidentWatcher.Models;

namespace BoeingIncidentWatcher.Storage;

internal sealed class TsvNewsStore : INewsStore
{
    private readonly object _syncRoot = new();
    private readonly string _stateFilePath;
    private readonly int _maxItems;
    private readonly IIncidentFilter _incidentFilter;
    private readonly List<NewsItem> _items;

    public TsvNewsStore(string stateFilePath, int maxItems, IIncidentFilter incidentFilter)
    {
        _stateFilePath = stateFilePath;
        _maxItems = maxItems;
        _incidentFilter = incidentFilter;
        EnsureDirectoryExists(_stateFilePath);
        _items = Load();
        Save();
    }

    public DateTimeOffset LastRefresh { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _items.Count;
            }
        }
    }

    public int Merge(IReadOnlyList<NewsItem> incoming)
    {
        lock (_syncRoot)
        {
            LastRefresh = DateTimeOffset.Now;
            LastError = string.Empty;

            var known = new HashSet<string>(_items.Select(x => x.Link), StringComparer.OrdinalIgnoreCase);
            var added = 0;

            foreach (var item in incoming)
            {
                if (known.Contains(item.Link))
                {
                    continue;
                }

                _items.Add(item);
                known.Add(item.Link);
                added++;
            }

            _items.Sort((left, right) => right.Published.CompareTo(left.Published));

            if (_items.Count > _maxItems)
            {
                _items.RemoveRange(_maxItems, _items.Count - _maxItems);
            }

            Save();
            return added;
        }
    }

    public void SetLastError(string error)
    {
        lock (_syncRoot)
        {
            LastRefresh = DateTimeOffset.Now;
            LastError = error;
        }
    }

    public List<NewsItem> GetItems()
    {
        lock (_syncRoot)
        {
            return _items.Select(x => x.Clone()).ToList();
        }
    }

    private List<NewsItem> Load()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new List<NewsItem>();
        }

        var items = new List<NewsItem>();
        foreach (var line in File.ReadAllLines(_stateFilePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 5)
            {
                continue;
            }

            items.Add(new NewsItem
            {
                Published = ParseDate(parts[0]),
                AddedAt = ParseDate(parts[1]),
                Title = DecodeField(parts[2]),
                Link = DecodeField(parts[3]),
                Description = DecodeField(parts[4]),
                Identifier = parts.Length > 5 ? DecodeField(parts[5]) : string.Empty,
                MatchReason = parts.Length > 6 ? DecodeField(parts[6]) : string.Empty,
                SourceName = parts.Length > 7 ? DecodeField(parts[7]) : "Google News"
            });
        }

        return items
            .Select(x => _incidentFilter.Apply(x))
            .Where(x => x != null)
            .OrderByDescending(x => x!.Published)
            .Take(_maxItems)
            .Select(x => x!)
            .ToList();
    }

    private void Save()
    {
        var lines = _items.Select(item =>
            item.Published.ToUniversalTime().ToString("o") + "\t" +
            item.AddedAt.ToUniversalTime().ToString("o") + "\t" +
            EncodeField(item.Title) + "\t" +
            EncodeField(item.Link) + "\t" +
            EncodeField(item.Description) + "\t" +
            EncodeField(item.Identifier) + "\t" +
            EncodeField(item.MatchReason) + "\t" +
            EncodeField(item.SourceName));

        File.WriteAllLines(_stateFilePath, lines);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static DateTimeOffset ParseDate(string value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }

    private static string EncodeField(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    private static string DecodeField(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch
        {
            return string.Empty;
        }
    }
}
