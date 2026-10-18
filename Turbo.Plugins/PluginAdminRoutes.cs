using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Admin;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Plugins;

/// <summary>
/// The admin endpoints and pages of the live plugins (<see cref="IPluginAdminRoutes"/>). A plugin's
/// table is built from its <see cref="IPluginAdmin"/> as it loads (<see cref="Build"/>), goes live
/// once it has started, and is taken away with it; while a new version loads the plugin answers
/// "reloading", and once taken away "off", so a stale tab is told rather than served the old one.
/// </summary>
internal sealed class PluginAdminRoutes : IPluginAdminRoutes
{
    /// <summary>The first segment of every node a plugin declares here: <c>plugin.&lt;key&gt;.&lt;name&gt;</c>.</summary>
    public const string NODE_ROOT = "plugin";

    private static readonly string[] METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    private readonly ConcurrentDictionary<string, Table> _live = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, PluginAdminState> _states = new(
        StringComparer.OrdinalIgnoreCase
    );

    public PluginAdminState StateOf(string key) =>
        _states.GetValueOrDefault(key, PluginAdminState.Unknown);

    public PluginAdminEndpoint? Match(
        string key,
        string method,
        string path,
        out IReadOnlyDictionary<string, string> route
    )
    {
        route = ImmutableDictionary<string, string>.Empty;

        if (StateOf(key) != PluginAdminState.Live || !_live.TryGetValue(key, out var table))
            return null;

        var segments = Segments(path);

        foreach (var endpoint in table.Endpoints)
        {
            if (
                !string.Equals(endpoint.Method, method, StringComparison.OrdinalIgnoreCase)
                || Bind(Segments(endpoint.Pattern), segments) is not { } values
            )
                continue;

            route = values;

            return endpoint;
        }

        return null;
    }

    public IReadOnlyList<PluginAdminPages> Pages() =>
        [
            .. _live
                .Where(x => StateOf(x.Key) == PluginAdminState.Live)
                .Select(x => x.Value.Pages)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
        ];

    /// <summary>
    /// The table a plugin's <see cref="IPluginAdmin"/> describes. Throws
    /// <see cref="InvalidOperationException"/> for a node, endpoint or page it declares twice, an
    /// endpoint or page guarded by a node it did not declare, or a method the API does not serve.
    /// </summary>
    public static Table Build(string key, string name, string version, IPluginAdmin admin)
    {
        var builder = new Builder(key.ToLowerInvariant());

        admin.Configure(builder);

        foreach (var page in builder.PageList)
            builder.Require(page.Node, $"page {page.Id}");

        return new Table(
            [.. builder.Endpoints],
            new PluginAdminPages(
                key,
                name,
                version,
                Guid.NewGuid().ToString("N")[..12],
                [.. builder.PageList],
                [.. builder.NodeList.Select(x => x.Node)]
            ),
            new Nodes([.. builder.NodeList])
        );
    }

    /// <summary>A new version of the plugin is loading: its calls wait for it.</summary>
    public void Reloading(string key)
    {
        _live.TryRemove(key, out _);
        _states[key] = PluginAdminState.Reloading;
    }

    /// <summary>Puts the plugin's table live; disposing the result takes it away ("off").</summary>
    public IDisposable Live(string key, Table table)
    {
        _live[key] = table;
        _states[key] = PluginAdminState.Live;

        return new Removal(this, key, table);
    }

    /// <summary>The plugin is turned off, failed or gone: what it served answers "off".</summary>
    public void Off(string key)
    {
        _live.TryRemove(key, out _);

        if (_states.ContainsKey(key))
            _states[key] = PluginAdminState.Off;
    }

    private static string[] Segments(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Dictionary<string, string>? Bind(string[] pattern, string[] path)
    {
        if (pattern.Length != path.Length)
            return null;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i].StartsWith('{') && pattern[i].EndsWith('}'))
                values[pattern[i][1..^1]] = Uri.UnescapeDataString(path[i]);
            else if (!string.Equals(pattern[i], path[i], StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return values;
    }

    /// <summary>One load's endpoints, pages and the permission source of its nodes.</summary>
    public sealed record Table(
        ImmutableArray<PluginAdminEndpoint> Endpoints,
        PluginAdminPages Pages,
        IPermissionNodeSource Nodes
    );

    private sealed class Nodes(ImmutableArray<PermissionNodeDefinition> nodes)
        : IPermissionNodeSource
    {
        public string? Prefix => NODE_ROOT;

        IEnumerable<PermissionNodeDefinition> IPermissionNodeSource.Nodes => nodes;

        public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
    }

    private sealed class Removal(PluginAdminRoutes owner, string key, Table table) : IDisposable
    {
        public void Dispose()
        {
            // Only the version it put live: a newer one may already have replaced it.
            if (owner._live.TryGetValue(key, out var live) && ReferenceEquals(live, table))
                owner.Off(key);
        }
    }

    private sealed class Builder(string key) : IPluginAdminBuilder
    {
        public List<PermissionNodeDefinition> NodeList { get; } = [];

        public List<PluginAdminEndpoint> Endpoints { get; } = [];

        public List<PluginAdminPage> PageList { get; } = [];

        public string Node(string name, string description)
        {
            var node = $"{NODE_ROOT}.{key}.{name}";

            if (!PermissionNodeFormat.IsValidNode(node))
                throw new InvalidOperationException(
                    $"The plugin {key}'s node '{name}' is not lowercase segments split by dots."
                );

            if (NodeList.Any(x => x.Node == node))
                throw new InvalidOperationException(
                    $"The plugin {key} declares the node {node} twice."
                );

            NodeList.Add(new PermissionNodeDefinition(node, description));

            return node;
        }

        public void Map(
            string method,
            string pattern,
            string node,
            Func<PluginAdminRequest, CancellationToken, Task<PluginAdminResult>> handler
        )
        {
            method = method.ToUpperInvariant();

            if (!METHODS.Contains(method))
                throw new InvalidOperationException(
                    $"The plugin {key} maps {method} {pattern}: the panel's API serves {string.Join(", ", METHODS)}."
                );

            Require(node, $"{method} {pattern}");

            var normalized = string.Join('/', Segments(pattern));

            if (
                Endpoints.Any(x =>
                    x.Method == method
                    && string.Equals(x.Pattern, normalized, StringComparison.OrdinalIgnoreCase)
                )
            )
                throw new InvalidOperationException(
                    $"The plugin {key} maps {method} {normalized} twice."
                );

            Endpoints.Add(new PluginAdminEndpoint(method, normalized, node, handler));
        }

        public void Page(PluginAdminPage page)
        {
            if (PageList.Any(x => string.Equals(x.Id, page.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    $"The plugin {key} declares the page {page.Id} twice."
                );

            PageList.Add(page);
        }

        public void Require(string node, string what)
        {
            if (NodeList.All(x => x.Node != node))
                throw new InvalidOperationException(
                    $"The plugin {key}'s {what} is guarded by {node}, which is not one of its own nodes ({NODE_ROOT}.{key}.*)."
                );
        }
    }
}
