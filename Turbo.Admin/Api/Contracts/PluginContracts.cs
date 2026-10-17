using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The server's plugins, and what the staff member may do with them: <see cref="CanManage"/>
/// reload and turn them off or on; <see cref="CanInstall"/> install a package, which also needs
/// the server to allow it (<see cref="UploadEnabled"/>). <see cref="Sources"/> are the GitHub
/// repositories plugins are linked to, checked every <see cref="CheckMinutes"/> (0: on request),
/// from the owners in <see cref="AllowedSources"/> (empty: any); <see cref="Job"/> is the check or
/// update running, or the last one.
/// </summary>
public sealed record PluginsResponse(
    IReadOnlyList<PluginView> Plugins,
    bool CanManage,
    bool CanInstall,
    bool UploadEnabled,
    long MaxUploadBytes,
    IReadOnlyList<PluginSourceView> Sources,
    PluginJobView? Job,
    IReadOnlyList<string> AllowedSources,
    int CheckMinutes
);

/// <summary>
/// One plugin. <see cref="State"/> is <c>loaded</c>, <c>failed</c>, <c>disabled</c> or
/// <c>not_loaded</c>; <see cref="Name"/> is null for a folder whose manifest could not be read.
/// <see cref="Versions"/> are the earlier versions kept to roll back to, newest first.
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
    DateTime? LoadedAtUtc,
    IReadOnlyList<string> Versions
)
{
    public static PluginView From(PluginSnapshot p, IReadOnlyList<string>? versions = null) =>
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
            p.LoadedAtUtc,
            versions ?? []
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

/// <summary>
/// A linked GitHub repository: what it follows (<c>latest</c> or <c>tag</c>), never its token,
/// only whether it has one, what is installed from it and what GitHub last offered.
/// </summary>
public sealed record PluginSourceView(
    string Id,
    string Repository,
    string Follow,
    string? Tag,
    string? Asset,
    bool HasToken,
    bool AutoUpdate,
    string? Key,
    string? InstalledTag,
    string? InstalledVersion,
    string? LatestTag,
    string? LatestUrl,
    DateTime? LatestPublishedAtUtc,
    DateTime? CheckedAtUtc,
    string? Error,
    bool UpdateAvailable
)
{
    public static PluginSourceView From(PluginSourceSnapshot x) =>
        new(
            x.Id,
            x.Repository,
            x.Follow,
            x.Tag,
            x.Asset,
            x.HasToken,
            x.AutoUpdate,
            x.Key,
            x.InstalledTag,
            x.InstalledVersion,
            x.LatestTag,
            x.LatestUrl,
            x.LatestPublishedAtUtc,
            x.CheckedAtUtc,
            x.Error,
            x.UpdateAvailable
        );
}

/// <summary>
/// A link as staff enter it. <see cref="Token"/> null keeps the saved one, empty removes it.
/// </summary>
public sealed record PluginSourceRequest(
    string? Repository,
    string? Follow,
    string? Tag,
    string? Asset,
    string? Token,
    bool AutoUpdate
)
{
    public PluginSourceInput ToInput() => new(Repository, Follow, Tag, Asset, Token, AutoUpdate);
}

/// <summary>Rolls a plugin back to a kept version.</summary>
public sealed record PluginRollbackRequest(string? Version);

/// <summary>
/// A plugin check or update, shaped as the asset jobs are (<c>kind</c> <c>plugin-check</c> or
/// <c>plugin-update</c>), so the panel shows it with the same job panel.
/// </summary>
public sealed record PluginJobView(
    Guid Id,
    string Kind,
    string Title,
    string Status,
    string Phase,
    int Total,
    int Done,
    int Failed,
    IReadOnlyList<string> Log,
    string? Result,
    string? Error,
    int PlayerId,
    DateTime StartedAt,
    DateTime? FinishedAt
)
{
    public static PluginJobView From(PluginJobSnapshot job) =>
        new(
            job.Id,
            job.Kind == PluginJobKind.Check ? "plugin-check" : "plugin-update",
            job.Title,
            job.Status.ToString().ToLowerInvariant(),
            job.Phase,
            job.Total,
            job.Done,
            0,
            [.. job.Log],
            job.Result,
            job.Error,
            job.PlayerId,
            job.StartedAt,
            job.FinishedAt
        );
}

/// <summary>What a link action did: the link after it and the job it started.</summary>
public sealed record PluginSourceActionResponse(
    bool Ok,
    string Message,
    PluginSourceView? Source,
    PluginJobView? Job
)
{
    public static PluginSourceActionResponse From(PluginSourceResult result) =>
        new(
            result.Ok,
            result.Message,
            result.Source is { } source ? PluginSourceView.From(source) : null,
            result.Job is { } job ? PluginJobView.From(job) : null
        );
}
