---
name: turbo-web-routes-admin
description: Serve HTTP from a Turbo plugin - player-facing web routes (IPluginWebRoute at /api/plugins/<key>/..., signed-in or anonymous, webhooks with RawBody signatures, api.http from the client module) and staff admin pages and endpoints drawn by the admin panel from a schema (IPluginAdmin with Node, Map and Page). Use for a site page's data, a webhook, a leaderboard API, or a staff page for the plugin.
---

# Web routes and admin pages

## When to use this skill

Players or a site ask the plugin for data over HTTP; a payment or game service calls it back; staff
need a page in the admin panel. Never host a web server of the plugin's own.

## A player-facing route

`npm run make route rares/values --anonymous` writes the shape. A public `IPluginWebRoute` is found
as the plugin loads and answers at `/api/plugins/<key>/<Pattern>` on the hotel's public web host
once the plugin has started (404 as it unloads, 503 while it reloads).

<!-- compiled as the plugin "demo" -->

```csharp
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Admin;
using Turbo.Contracts.Plugins.Web;
using Turbo.Primitives.Players.Permissions;

namespace Demo;

public sealed class DemoPlugin : TurboPlugin
{
    protected override void Services(IServiceCollection services) =>
        services.AddSingleton<ScoreBook>().AddSingleton<IPluginAdmin, DemoAdmin>();
}

public sealed class ScoreBook
{
    private readonly ConcurrentDictionary<int, int> _best = [];

    public void Add(int player, int points) => _best.AddOrUpdate(player, points, (_, old) => old > points ? old : points);

    public IReadOnlyList<KeyValuePair<int, int>> Top(int take) => [.. _best.OrderByDescending(x => x.Value).Take(take)];

    public void Clear() => _best.Clear();
}

/// <summary>GET /api/plugins/demo/top: the ten best, for anyone.</summary>
public sealed class TopRoute(ScoreBook scores) : IPluginWebRoute
{
    public string Method => "GET";

    public string Pattern => "top";

    public bool Anonymous => true;

    public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct) =>
        Task.FromResult(PluginWebResult.Ok(scores.Top(10).Select(x => new { player = x.Key, points = x.Value })));
}

/// <summary>POST /api/plugins/demo/scores/{game}: the signed-in player's score.</summary>
public sealed class SubmitRoute(ScoreBook scores) : IPluginWebRoute
{
    public string Method => "POST";

    public string Pattern => "scores/{game}";

    public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
    {
        var points = request.Body?.GetProperty("points").GetInt32() ?? 0;

        if (points is < 0 or > 1000)
            return Task.FromResult(PluginWebResult.BadRequest("Points are 0 to 1000."));

        scores.Add(request.PlayerId!.Value, points);

        return Task.FromResult(PluginWebResult.NoContent());
    }
}

/// <summary>DELETE /api/plugins/demo/scores: players holding the node clear the board.</summary>
[RequiresPermission("demo.scores.reset")]
public sealed class ResetRoute(ScoreBook scores) : IPluginWebRoute
{
    public string Method => "DELETE";

    public string Pattern => "scores";

    public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
    {
        scores.Clear();

        return Task.FromResult(PluginWebResult.NoContent());
    }
}
```

- `Pattern`: literal or `{name}` segments under the plugin's prefix (`request.Route["game"]`); one
  that could leave the prefix, or one mapped twice, fails the load.
- Signed in by default (`request.PlayerId` set: the client's session token or the site's cookie);
  `Anonymous => true` for anyone. `[RequiresPermission]` needs a player, so not on anonymous routes.
- `PluginWebRequest`: `Method`, `Path`, `Route`, `Query`, `Body` (`JsonElement?`), `RawBody`,
  `PlayerId`, `RemoteAddress`, `Headers` (credential headers left out unless `CredentialHeaders`).
- Answers: `PluginWebResult.Ok(value)`, `NoContent()`, `Accepted()`, `BadRequest`, `Forbidden`,
  `NotFound`, `new(status, Error: "...")`. Errors are `{"error": ...}`.
- Webhooks: verify the signature over `request.RawBody` (never re-serialised `Body`) with
  `CryptographicOperations.FixedTimeEquals`.
- The host applies body/time caps, rate limits per player and address, and CORS from the hotel's sites.
- Client module: `await api.http.post('scores/pool', { points: 5 })` (the session is attached for you).

## Staff pages: IPluginAdmin

Registered in the plugin's services; the panel draws its pages from the schema (no plugin code runs
in the panel). Endpoints are on the panel's host, `/api/plugins/<key>/api/...`, with the panel's
sign-in, audit (every call but GET) and the node checked.

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Admin;

namespace Demo;

/// <summary>Registered in DemoPlugin.Services above (AddSingleton&lt;IPluginAdmin, DemoAdmin&gt;).</summary>
public sealed class DemoAdmin(ScoreBook scores) : IPluginAdmin
{
    public void Configure(IPluginAdminBuilder admin)
    {
        var view = admin.Node("scores.view", "Demo: see the score board"); // plugin.demo.scores.view

        admin.Map("GET", "scores", view, ListAsync);
        admin.Page(new PluginAdminPage(
            "scores",
            "Scores",
            view,
            [
                new PluginAdminBlock("table", "Best scores", Endpoint: "scores",
                    Columns: [new PluginAdminColumn("player", "Player"), new PluginAdminColumn("points", "Points")]),
            ],
            "The best score of each player.",
            Icon: "trophy"));
    }

    private Task<PluginAdminResult> ListAsync(PluginAdminRequest request, CancellationToken ct) =>
        Task.FromResult(PluginAdminResult.Ok(scores.Top(50).Select(x => new { player = x.Key, points = x.Value })));
}
```

Blocks: `table` (rows from an endpoint, `ItemPage` to open one), `detail`, `form` (`Fields`,
`Submit: new PluginAdminAction(label, method, endpoint)`), `text`. Field types: `text`, `number`,
`bool`, ...; column types `text`, `bool`, `badge`, `time`. Nodes from `admin.Node` are
`plugin.<key>.<name>`, granted to no one by default.

## Check

`npm run check`; guides `plugin-web-routes.md`, `admin-panel.md` (Plugins). Test a route by calling
`HandleAsync` with a `new PluginWebRequest(...)`; an admin handler likewise.
