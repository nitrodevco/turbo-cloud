using System.Collections.Generic;
using System.Collections.Immutable;

namespace Turbo.Contracts.Plugins.Web;

/// <summary>
/// The host's side of <see cref="IPluginWebRoute"/>: the routes of the plugins live now, which the
/// public web host serves. Each load replaces a plugin's whole table at once.
/// </summary>
public interface IPluginWebRoutes
{
    /// <summary>Whether a new version of the plugin is loading: its calls are asked to come again.</summary>
    bool IsReloading(string key);

    /// <summary>The live route of the plugin for the call, with its route values; null when none matches.</summary>
    PluginWebEndpoint? Match(
        string key,
        string method,
        string path,
        out IReadOnlyDictionary<string, string> route
    );

    /// <summary>The methods the plugin's live routes answer at the path, for a preflight; empty when none.</summary>
    IReadOnlyList<string> MethodsAt(string key, string path);
}

/// <summary>
/// One route a plugin serves: its method, its pattern relative to the plugin's prefix, whether
/// anyone may call it, the permission nodes it needs (any one of them; empty for none) and the
/// route itself.
/// </summary>
public sealed record PluginWebEndpoint(
    string Key,
    string Method,
    string Pattern,
    bool Anonymous,
    ImmutableArray<string> Nodes,
    IPluginWebRoute Route
);
