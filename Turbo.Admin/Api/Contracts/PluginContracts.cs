using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The server's plugins, and what the staff member may do with them: <see cref="CanManage"/>
/// reload and turn them off or on; <see cref="CanInstall"/> install a package, which also needs
/// the server to allow it (<see cref="UploadEnabled"/>).
/// </summary>
public sealed record PluginsResponse(
    IReadOnlyList<PluginView> Plugins,
    bool CanManage,
    bool CanInstall,
    bool UploadEnabled,
    long MaxUploadBytes
);

/// <summary>
/// One plugin. <see cref="State"/> is <c>loaded</c>, <c>failed</c>, <c>disabled</c> or
/// <c>not_loaded</c>; <see cref="Name"/> is null for a folder whose manifest could not be read.
/// </summary>
public sealed record PluginView(
    string Key,
    string? Name,
    string? Version,
    string? Author,
    string Folder,
    bool Dev,
    string State,
    string? Error,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<int> IncomingHeaders,
    IReadOnlyList<int> OutgoingHeaders,
    IReadOnlyList<string> Migrations,
    DateTime? LoadedAtUtc
)
{
    public static PluginView From(PluginSnapshot p) =>
        new(
            p.Key,
            p.Name,
            p.Version,
            p.Author,
            p.Folder,
            p.Dev,
            StateName(p.State),
            p.Error,
            [.. p.Dependencies],
            [.. p.Capabilities],
            [.. p.IncomingHeaders],
            [.. p.OutgoingHeaders],
            [.. p.Migrations],
            p.LoadedAtUtc
        );

    public static string StateName(PluginStateType state) =>
        state switch
        {
            PluginStateType.Loaded => "loaded",
            PluginStateType.Failed => "failed",
            PluginStateType.Disabled => "disabled",
            _ => "not_loaded",
        };
}

/// <summary>What an action did, and the plugin after it (null for a rescan, or a refused package).</summary>
public sealed record PluginActionResponse(bool Ok, string Message, PluginView? Plugin)
{
    public static PluginActionResponse From(PluginActionResult result) =>
        new(
            result.Ok,
            result.Message,
            result.Plugin is { } plugin ? PluginView.From(plugin) : null
        );
}
