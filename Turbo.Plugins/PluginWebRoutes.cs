using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins.Web;
using Turbo.Primitives.Players.Permissions;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins;

/// <summary>
/// The player-facing web routes of the live plugins (<see cref="IPluginWebRoutes"/>). A plugin's
/// table is found in its assembly as it loads (<see cref="Build"/>), goes live once it has
/// started, and is taken away as it stops; while a new version loads the plugin answers
/// "reloading", and once taken away it has no routes at all.
/// </summary>
internal sealed class PluginWebRoutes : IPluginWebRoutes
{
    private static readonly string[] METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    private readonly ConcurrentDictionary<string, ImmutableArray<PluginWebEndpoint>> _live = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, byte> _reloading = new(
        StringComparer.OrdinalIgnoreCase
    );

    public bool IsReloading(string key) => _reloading.ContainsKey(key);

    public PluginWebEndpoint? Match(
        string key,
        string method,
        string path,
        out IReadOnlyDictionary<string, string> route
    )
    {
        route = ImmutableDictionary<string, string>.Empty;

        if (!_live.TryGetValue(key, out var table))
            return null;

        var segments = Segments(path);

        foreach (var endpoint in table)
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

    public IReadOnlyList<string> MethodsAt(string key, string path)
    {
        if (!_live.TryGetValue(key, out var table))
            return [];

        var segments = Segments(path);

        return
        [
            .. table
                .Where(x => Bind(Segments(x.Pattern), segments) is not null)
                .Select(x => x.Method)
                .Distinct(),
        ];
    }

    /// <summary>
    /// The routes <paramref name="asm"/> declares (public <see cref="IPluginWebRoute"/>s), each
    /// built with <paramref name="services"/>. Throws <see cref="InvalidOperationException"/> for a
    /// method the host does not serve, a pattern that leaves the plugin's prefix, a route mapped
    /// twice, or an anonymous route that requires a permission.
    /// </summary>
    public static ImmutableArray<PluginWebEndpoint> Build(
        string key,
        Assembly asm,
        IServiceProvider services
    )
    {
        var endpoints = new List<PluginWebEndpoint>();

        foreach (var concrete in AssemblyExplorer.FindAssignees(asm, typeof(IPluginWebRoute)))
        {
            var route = (IPluginWebRoute)ActivatorUtilities.CreateInstance(services, concrete);
            var method = (route.Method ?? string.Empty).ToUpperInvariant();
            var name = concrete.Name;

            if (!METHODS.Contains(method))
                throw new InvalidOperationException(
                    $"The plugin {key}'s web route {name} answers {method}: the host serves {string.Join(", ", METHODS)}."
                );

            var pattern = Normalize(key, name, route.Pattern ?? string.Empty);
            ImmutableArray<string> nodes =
            [
                .. concrete.GetCustomAttribute<RequiresPermissionAttribute>()?.Nodes ?? [],
            ];

            if (route.Anonymous && nodes.Length > 0)
                throw new InvalidOperationException(
                    $"The plugin {key}'s web route {name} is anonymous but requires {string.Join(" or ", nodes)}: a permission needs a player."
                );

            if (
                endpoints.Any(x =>
                    x.Method == method
                    && string.Equals(x.Pattern, pattern, StringComparison.OrdinalIgnoreCase)
                )
            )
                throw new InvalidOperationException(
                    $"The plugin {key} maps the web route {method} {pattern} twice."
                );

            endpoints.Add(
                new PluginWebEndpoint(key, method, pattern, route.Anonymous, nodes, route)
            );
        }

        return [.. endpoints];
    }

    /// <summary>A new version of the plugin is loading: its calls are asked to come again.</summary>
    public void Reloading(string key)
    {
        _live.TryRemove(key, out _);
        _reloading[key] = 0;
    }

    /// <summary>Puts the plugin's routes live; disposing the result takes them away.</summary>
    public IDisposable Live(string key, ImmutableArray<PluginWebEndpoint> table)
    {
        _live[key] = table;
        _reloading.TryRemove(key, out _);

        return new Removal(this, key, table);
    }

    /// <summary>The plugin is stopping, turned off, failed or gone: it has no routes.</summary>
    public void Off(string key)
    {
        _live.TryRemove(key, out _);
        _reloading.TryRemove(key, out _);
    }

    /// <summary>
    /// The pattern as segments joined by <c>/</c>. A pattern that could address anything outside
    /// the plugin's own prefix (<c>..</c>, <c>.</c>, a scheme, a query, a leading <c>api</c>
    /// segment that reads as a core path) is refused.
    /// </summary>
    private static string Normalize(string key, string name, string pattern)
    {
        var segments = Segments(pattern);

        if (
            segments.Length == 0
            || pattern.Contains("://", StringComparison.Ordinal)
            || pattern.IndexOfAny(['?', '#', '\\', '%']) >= 0
            || segments.Any(x => x is "." or "..")
            || string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase)
            || segments.Any(x => (x.Contains('{') || x.Contains('}')) && !IsValue(x))
        )
            throw new InvalidOperationException(
                $"The plugin {key}'s web route {name} has the pattern '{pattern}': it must be relative segments under /api/plugins/{key}/, such as scores/{{game}}."
            );

        return string.Join('/', segments);
    }

    /// <summary>A <c>{name}</c> segment: letters, digits and <c>_</c> between the braces.</summary>
    private static bool IsValue(string segment) =>
        segment.Length > 2
        && segment[0] == '{'
        && segment[^1] == '}'
        && segment[1..^1].All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

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

    private sealed class Removal(
        PluginWebRoutes owner,
        string key,
        ImmutableArray<PluginWebEndpoint> table
    ) : IDisposable
    {
        public void Dispose()
        {
            // Only the version it put live: a newer one may already have replaced it.
            if (owner._live.TryGetValue(key, out var live) && live == table)
                owner._live.TryRemove(key, out _);
        }
    }
}
