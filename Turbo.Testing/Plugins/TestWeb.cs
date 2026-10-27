using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Admin;
using Turbo.Contracts.Plugins.Web;

namespace Turbo.Testing;

/// <summary>
/// The plugins' player-facing web routes (turbo-cloud docs/plugin-web-routes.md), called as the
/// hotel's site calls them: matched by method and path under <c>/plugins/&lt;key&gt;/</c>, a route
/// that is not anonymous refused to no one signed in (401) and to a player without one of its
/// nodes (403), and otherwise answered by the plugin.
/// </summary>
public sealed class TestWeb
{
    private readonly TestHotel _hotel;

    internal TestWeb(TestHotel hotel) => _hotel = hotel;

    public Task<PluginWebResult> GetAsync(
        string path,
        TestPlayer? player = null,
        CancellationToken ct = default
    ) => SendAsync("GET", path, null, player, ct);

    public Task<PluginWebResult> PostAsync(
        string path,
        object? body,
        TestPlayer? player = null,
        CancellationToken ct = default
    ) => SendAsync("POST", path, body, player, ct);

    /// <summary>
    /// <paramref name="method"/> <paramref name="path"/> (<c>/plugins/skills/top?n=5</c>, or
    /// <c>skills/top</c>) with <paramref name="body"/> as JSON, signed in as <paramref name="player"/>.
    /// </summary>
    public async Task<PluginWebResult> SendAsync(
        string method,
        string path,
        object? body = null,
        TestPlayer? player = null,
        CancellationToken ct = default
    )
    {
        var (key, rest, query) = Split(path);
        var endpoint = _hotel.WebRoutes.Match(key, method, rest, out var route);

        if (endpoint is null)
            return new PluginWebResult(404, Error: $"No route {method} {rest} of {key}.");

        if (!endpoint.Anonymous && player is null)
            return new PluginWebResult(401, Error: "Not signed in.");

        if (
            player is not null
            && endpoint.Nodes.Length > 0
            && !endpoint.Nodes.Any(node => _hotel.Grants(player.Fake, node))
        )
            return new PluginWebResult(403, Error: "Missing a node the route needs.");

        var json = body is null ? (JsonElement?)null : JsonSerializer.SerializeToElement(body);

        return await endpoint
            .Route.HandleAsync(
                new PluginWebRequest(
                    method,
                    rest,
                    route,
                    query,
                    json,
                    player?.Id.Value,
                    "127.0.0.1"
                )
                {
                    RawBody = json is { } raw ? JsonSerializer.SerializeToUtf8Bytes(raw) : default,
                },
                ct
            )
            .ConfigureAwait(false);
    }

    internal static (string Key, string Path, IReadOnlyDictionary<string, string> Query) Split(
        string path
    )
    {
        var at = path.IndexOf('?');
        var query = at < 0 ? "" : path[(at + 1)..];
        var parts = (at < 0 ? path : path[..at]).Trim('/').Split('/');

        if (parts.Length > 0 && parts[0] == "plugins")
            parts = parts[1..];

        return (
            parts[0],
            "/" + string.Join('/', parts[1..]),
            query
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split('=', 2))
                .ToDictionary(
                    x => Uri.UnescapeDataString(x[0]),
                    x => x.Length > 1 ? Uri.UnescapeDataString(x[1]) : ""
                )
        );
    }
}

/// <summary>
/// The plugins' admin panel endpoints (<c>IPluginAdmin</c>, turbo-cloud docs/admin-panel.md), called
/// as the panel calls them: <c>GET events/history?page=2</c> by a staff member, refused (403)
/// without the endpoint's node. The pages each plugin declares are in <see cref="Pages"/>.
/// </summary>
public sealed class TestAdmin
{
    private readonly TestHotel _hotel;

    internal TestAdmin(TestHotel hotel) => _hotel = hotel;

    /// <summary>The admin pages of the live plugins.</summary>
    public IReadOnlyList<PluginAdminPage> Pages =>
        [.. _hotel.AdminRoutes.Pages().SelectMany(p => p.Pages)];

    /// <summary>The permission nodes the live plugins' admin endpoints declare (<c>plugin.&lt;key&gt;.&lt;name&gt;</c>).</summary>
    public IReadOnlyList<string> Nodes => [.. _hotel.AdminRoutes.Pages().SelectMany(p => p.Nodes)];

    /// <summary>
    /// <paramref name="method"/> <paramref name="path"/> (<c>events/history?page=2</c>) with
    /// <paramref name="body"/> as JSON, by <paramref name="staff"/> (any node when null).
    /// </summary>
    public async Task<PluginAdminResult> CallAsync(
        string method,
        string path,
        object? body = null,
        TestPlayer? staff = null,
        CancellationToken ct = default
    )
    {
        var (key, rest, query) = TestWeb.Split(path);
        var endpoint = _hotel.AdminRoutes.Match(key, method, rest, out var route);

        if (endpoint is null)
            return PluginAdminResult.NotFound($"No endpoint {method} {rest} of {key}.");

        if (staff is not null && !_hotel.Grants(staff.Fake, endpoint.Node))
            return new PluginAdminResult(403, Error: $"Needs {endpoint.Node}.");

        return await endpoint
            .Handler(
                new PluginAdminRequest(
                    method,
                    rest,
                    route,
                    query,
                    body is null ? null : JsonSerializer.SerializeToElement(body),
                    staff?.Id.Value ?? 1
                ),
                ct
            )
            .ConfigureAwait(false);
    }

    public Task<PluginAdminResult> GetAsync(
        string path,
        TestPlayer? staff = null,
        CancellationToken ct = default
    ) => CallAsync("GET", path, null, staff, ct);

    public Task<PluginAdminResult> PostAsync(
        string path,
        object? body,
        TestPlayer? staff = null,
        CancellationToken ct = default
    ) => CallAsync("POST", path, body, staff, ct);
}
