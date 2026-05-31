using System.Net;
using System.Text;
using System.Text.Json;
using BoeingIncidentWatcher.Configuration;
using BoeingIncidentWatcher.Storage;

namespace BoeingIncidentWatcher.Dashboard;

internal sealed class DashboardServer
{
    private readonly string _url;
    private readonly INewsStore _newsStore;
    private readonly Settings _settings;
    private readonly HttpListener _listener;

    public DashboardServer(string url, INewsStore newsStore, Settings settings)
    {
        _url = url.EndsWith("/") ? url : url + "/";
        _newsStore = newsStore;
        _settings = settings;
        _listener = new HttpListener();
        _listener.Prefixes.Add(_url);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();

        using (cancellationToken.Register(() => _listener.Stop()))
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                await HandleRequestAsync(context);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;

        if (path.Equals("/api/news", StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(context, "application/json; charset=utf-8", BuildNewsJson());
            return;
        }

        if (path.Equals("/api/refresh-info", StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(context, "application/json; charset=utf-8", BuildRefreshInfoJson());
            return;
        }

        await WriteResponseAsync(context, "text/html; charset=utf-8", BuildDashboardHtml());
    }

    private string BuildNewsJson()
    {
        var payload = new
        {
            items = _newsStore.GetItems().Select(item => new
            {
                title = item.Title,
                link = item.Link,
                description = item.Description,
                sourceName = item.SourceName,
                identifier = item.Identifier,
                matchReason = item.MatchReason,
                published = FormatDate(item.Published),
                addedAt = FormatDate(item.AddedAt)
            }),
            lastRefresh = FormatDate(_newsStore.LastRefresh),
            lastError = _newsStore.LastError ?? string.Empty,
            query = _settings.GoogleQuery
        };

        return JsonSerializer.Serialize(payload);
    }

    private string BuildRefreshInfoJson()
    {
        return JsonSerializer.Serialize(new
        {
            lastRefresh = FormatDate(_newsStore.LastRefresh),
            lastError = _newsStore.LastError ?? string.Empty
        });
    }

    private static async Task WriteResponseAsync(HttpListenerContext context, string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
    }

    private static string FormatDate(DateTimeOffset value)
    {
        return value == DateTimeOffset.MinValue ? string.Empty : value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    private string BuildDashboardHtml()
    {
        return """
<!doctype html>
<html lang="uk">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Boeing Incident Dashboard</title>
  <style>
    :root {
      --bg: #f5f2ec;
      --ink: #18211f;
      --muted: #66716c;
      --line: #d8d0c3;
      --panel: #fffaf0;
      --accent: #b7382d;
      --accent-2: #1f6f62;
      --shadow: rgba(28, 37, 34, 0.12);
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      font-family: Georgia, 'Times New Roman', serif;
      background:
        linear-gradient(135deg, rgba(31,111,98,0.12), transparent 36%),
        radial-gradient(circle at top right, rgba(183,56,45,0.12), transparent 34%),
        var(--bg);
      color: var(--ink);
    }

    .shell {
      width: min(1180px, calc(100% - 32px));
      margin: 0 auto;
      padding: 28px 0 42px;
    }

    header {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 18px;
      align-items: end;
      padding-bottom: 22px;
      border-bottom: 1px solid var(--line);
    }

    h1 {
      margin: 0;
      font-size: clamp(34px, 5vw, 72px);
      line-height: 0.95;
      letter-spacing: 0;
      max-width: 780px;
    }

    .meta {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
      justify-content: flex-end;
      font-family: 'Segoe UI', Tahoma, sans-serif;
      color: var(--muted);
      font-size: 14px;
    }

    .pill {
      border: 1px solid var(--line);
      border-radius: 999px;
      padding: 8px 12px;
      background: rgba(255,250,240,0.75);
    }

    .stats {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 12px;
      margin: 22px 0;
      font-family: 'Segoe UI', Tahoma, sans-serif;
    }

    .stat {
      background: rgba(255,250,240,0.82);
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 16px;
      box-shadow: 0 10px 28px var(--shadow);
    }

    .stat strong {
      display: block;
      font-size: 26px;
      margin-bottom: 4px;
    }

    .stat span {
      color: var(--muted);
      font-size: 13px;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .toolbar {
      display: flex;
      gap: 10px;
      align-items: center;
      margin-bottom: 16px;
      font-family: 'Segoe UI', Tahoma, sans-serif;
    }

    input {
      width: 100%;
      min-height: 42px;
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 0 12px;
      background: rgba(255,250,240,0.9);
      color: var(--ink);
      font-size: 15px;
    }

    button {
      min-height: 42px;
      border: 0;
      border-radius: 8px;
      padding: 0 16px;
      background: var(--ink);
      color: #fffaf0;
      cursor: pointer;
      white-space: nowrap;
      font-weight: 700;
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(290px, 1fr));
      gap: 14px;
    }

    .card {
      display: flex;
      flex-direction: column;
      min-height: 260px;
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 18px;
      box-shadow: 0 12px 30px var(--shadow);
    }

    .card time {
      color: var(--accent-2);
      font-family: 'Segoe UI', Tahoma, sans-serif;
      font-size: 13px;
      font-weight: 700;
    }

    .identifier {
      display: inline-flex;
      width: fit-content;
      margin-top: 10px;
      padding: 5px 8px;
      border-radius: 6px;
      background: rgba(31,111,98,0.1);
      color: var(--accent-2);
      font-family: 'Segoe UI', Tahoma, sans-serif;
      font-size: 12px;
      font-weight: 800;
      text-transform: uppercase;
    }

    .source {
      margin-top: 8px;
      color: var(--muted);
      font-family: 'Segoe UI', Tahoma, sans-serif;
      font-size: 12px;
      font-weight: 700;
    }

    .card h2 {
      margin: 12px 0 10px;
      font-size: 23px;
      line-height: 1.12;
      letter-spacing: 0;
    }

    .card p {
      margin: 0 0 18px;
      color: var(--muted);
      font-family: 'Segoe UI', Tahoma, sans-serif;
      line-height: 1.5;
    }

    .card a {
      margin-top: auto;
      color: var(--accent);
      font-family: 'Segoe UI', Tahoma, sans-serif;
      font-weight: 800;
      text-decoration: none;
    }

    .empty {
      padding: 28px;
      border: 1px dashed var(--line);
      border-radius: 8px;
      background: rgba(255,250,240,0.68);
      font-family: 'Segoe UI', Tahoma, sans-serif;
      color: var(--muted);
    }

    .error {
      color: var(--accent);
      font-family: 'Segoe UI', Tahoma, sans-serif;
      margin-bottom: 12px;
      min-height: 20px;
    }

    @media (max-width: 720px) {
      header { grid-template-columns: 1fr; }
      .meta { justify-content: flex-start; }
      .stats { grid-template-columns: 1fr; }
      .toolbar { flex-direction: column; align-items: stretch; }
      button { width: 100%; }
    }
  </style>
</head>
<body>
  <main class="shell">
    <header>
      <div>
        <h1>Boeing Incident Monitor</h1>
      </div>
      <div class="meta">
        <span class="pill">Query: <strong id="query">...</strong></span>
        <span class="pill">Auto refresh: 30s</span>
      </div>
    </header>

    <section class="stats">
      <div class="stat"><strong id="total">0</strong><span>News items</span></div>
      <div class="stat"><strong id="latest">-</strong><span>Latest publish date</span></div>
      <div class="stat"><strong id="lastRefresh">-</strong><span>Last checked</span></div>
    </section>

    <div class="toolbar">
      <input id="filter" type="search" placeholder="Filter dashboard...">
      <button id="reload">Refresh</button>
    </div>

    <div id="error" class="error"></div>
    <section id="news" class="grid"></section>
  </main>

  <script>
    var items = [];
    var filterInput = document.getElementById('filter');

    function escapeHtml(value) {
      return String(value || '').replace(/[&<>"']/g, function (match) {
        return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[match];
      });
    }

    function loadNews() {
      fetch('/api/news')
        .then(function (response) { return response.json(); })
        .then(function (data) {
          items = data.items || [];
          document.getElementById('query').textContent = data.query || '-';
          document.getElementById('total').textContent = items.length;
          document.getElementById('lastRefresh').textContent = data.lastRefresh || '-';
          document.getElementById('latest').textContent = items.length ? items[0].published : '-';
          document.getElementById('error').textContent = data.lastError ? 'Last refresh error: ' + data.lastError : '';
          render();
        })
        .catch(function (error) {
          document.getElementById('error').textContent = 'Dashboard API error: ' + error.message;
        });
    }

    function render() {
      var needle = filterInput.value.toLowerCase();
      var visible = items.filter(function (item) {
        return !needle || (item.title + ' ' + item.description + ' ' + item.sourceName).toLowerCase().indexOf(needle) >= 0;
      });

      var container = document.getElementById('news');
      if (!visible.length) {
        container.innerHTML = '<div class="empty">No matching news yet. The service will keep checking configured sources.</div>';
        return;
      }

      container.innerHTML = visible.map(function (item) {
        return '<article class="card">' +
          '<time>' + escapeHtml(item.published || 'Unknown date') + '</time>' +
          (item.identifier ? '<span class="identifier">' + escapeHtml(item.identifier) + '</span>' : '') +
          (item.sourceName ? '<span class="source">' + escapeHtml(item.sourceName) + '</span>' : '') +
          '<h2>' + escapeHtml(item.title) + '</h2>' +
          '<p>' + escapeHtml(item.description || 'No summary available.') + '</p>' +
          '<a href="' + escapeHtml(item.link) + '" target="_blank" rel="noopener">Open source</a>' +
          '</article>';
      }).join('');
    }

    document.getElementById('reload').addEventListener('click', loadNews);
    filterInput.addEventListener('input', render);
    loadNews();
    setInterval(loadNews, 30000);
  </script>
</body>
</html>
""";
    }
}
