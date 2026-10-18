using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Admin;

/// <summary>
/// The host's side of <see cref="IPluginAdmin"/>: the endpoints and pages of the plugins live
/// now, which the admin API serves. Each load replaces a plugin's whole table at once.
/// </summary>
public interface IPluginAdminRoutes
{
    public PluginAdminState StateOf(string key);

    /// <summary>The live endpoint of the plugin for the call, with its route values; null when none matches.</summary>
    public PluginAdminEndpoint? Match(
        string key,
        string method,
        string path,
        out IReadOnlyDictionary<string, string> route
    );

    /// <summary>The pages of every live plugin.</summary>
    public IReadOnlyList<PluginAdminPages> Pages();
}

/// <summary>Where a plugin's admin extensions stand: never seen, turned off or taken away, being reloaded, or live.</summary>
public enum PluginAdminState
{
    Unknown,
    Off,
    Reloading,
    Live,
}

/// <summary>One endpoint a plugin serves (<see cref="IPluginAdminBuilder.Map"/>).</summary>
public sealed record PluginAdminEndpoint(
    string Method,
    string Pattern,
    string Node,
    Func<PluginAdminRequest, CancellationToken, Task<PluginAdminResult>> Handler
);

/// <summary>
/// A live plugin's pages and nodes. <see cref="Revision"/> changes with every load, so a panel
/// tab drawn from an earlier one knows it is out of date.
/// </summary>
public sealed record PluginAdminPages(
    string Key,
    string Name,
    string Version,
    string Revision,
    IReadOnlyList<PluginAdminPage> Pages,
    IReadOnlyList<string> Nodes
);
