using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The server's plugins, and what the staff member may do with them: <see cref="CanManage"/>
/// reload and turn them off or on; <see cref="CanInstall"/> install a package, which also needs
/// the server to allow it (<see cref="UploadEnabled"/>); <see cref="CanUninstall"/> uninstall one. <see cref="Sources"/> are the GitHub
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
    int CheckMinutes,
    bool CanUninstall
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
/// Uninstalls a plugin: <see cref="Mode"/> <c>keep-data</c> or <c>delete-data</c>, and
/// <see cref="Confirm"/> its key typed again, as it is written.
/// </summary>
public sealed record PluginUninstallRequest(string? Mode, string? Confirm)
{
    public const string KEEP_DATA = "keep-data";
    public const string DELETE_DATA = "delete-data";

    /// <summary>The mode asked for; null for anything else.</summary>
    public PluginUninstallMode? ParsedMode =>
        Mode switch
        {
            KEEP_DATA => PluginUninstallMode.KeepData,
            DELETE_DATA => PluginUninstallMode.DeleteData,
            _ => null,
        };
}

/// <summary>
/// The dry run of uninstalling a plugin: the files and links that go with its code, the data that
/// goes when asked (<see cref="Data"/>), what could not be looked at, and why it can't be
/// uninstalled now (<see cref="Refusal"/>, null when it can).
/// </summary>
public sealed record PluginUninstallPlanView(
    PluginView Plugin,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Sources,
    IReadOnlyList<PluginDataItemView> Data,
    IReadOnlyList<string> Notes,
    string? Refusal
)
{
    public static PluginUninstallPlanView From(PluginUninstallPlan plan) =>
        new(
            PluginView.From(plan.Plugin),
            [.. plan.Files],
            [.. plan.Sources],
            [.. plan.Data.Select(PluginDataItemView.From)],
            [.. plan.Notes],
            plan.Refusal
        );
}

/// <summary>
/// One thing of a plugin's data: <see cref="Kind"/> <c>table</c> (<see cref="Count"/> its rows),
/// <c>setting</c> (<see cref="Detail"/> its value, null for a secret), <c>grant</c>
/// (<see cref="Count"/> its assignments, <see cref="Detail"/> who holds them), or <c>variable</c> or
/// <c>text</c> (an external variable or text it brought, still as it gave it; <see cref="Detail"/> its
/// value). A kind added later is named in lowercase, never taken for another.
/// </summary>
public sealed record PluginDataItemView(string Kind, string Name, long Count, string? Detail)
{
    public static PluginDataItemView From(PluginDataItem item) =>
        new(
            item.Kind switch
            {
                PluginDataKind.Table => "table",
                PluginDataKind.Setting => "setting",
                PluginDataKind.Grant => "grant",
                PluginDataKind.Variable => "variable",
                PluginDataKind.Text => "text",
                _ => item.Kind.ToString().ToLowerInvariant(),
            },
            item.Name,
            item.Count,
            item.Detail
        );
}

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
