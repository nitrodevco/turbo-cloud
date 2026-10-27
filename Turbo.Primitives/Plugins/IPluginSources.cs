using System;
using System.Collections.Immutable;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// Plugins installed from GitHub releases and kept up to date: a linked repository is checked for
/// a newer release on a schedule (<c>Turbo:Plugin:UpdateCheckMinutes</c>) or when staff ask, and
/// an update downloads the release's package and installs it as an uploaded one is. Checks and
/// updates run as jobs (<see cref="Job"/>). Who may do which is the caller's to check.
/// </summary>
public interface IPluginSources
{
    /// <summary>The GitHub owners plugins may be linked from (<c>Turbo:Plugin:AllowedSources</c>); empty allows any.</summary>
    ImmutableArray<string> AllowedOwners { get; }

    /// <summary>Minutes between scheduled checks; 0 when only staff check.</summary>
    int CheckMinutes { get; }

    ImmutableArray<PluginSourceSnapshot> List();

    /// <summary>The job running, or the last one; null before the first.</summary>
    PluginJobSnapshot? Job { get; }

    /// <summary>Links a repository and starts installing its release.</summary>
    PluginSourceResult Add(PluginSourceInput input, int player);

    /// <summary>Changes what a link follows; null when there is no such link.</summary>
    PluginSourceResult? Edit(string id, PluginSourceInput input);

    /// <summary>Unlinks a repository; its plugin stays installed. Null when there is no such link.</summary>
    PluginSourceResult? Remove(string id);

    /// <summary>Starts a check of one link, or of all (<paramref name="id"/> null). Null when there is no such link.</summary>
    PluginSourceResult? Check(string? id, int player);

    /// <summary>Starts installing the release the link follows. Null when there is no such link.</summary>
    PluginSourceResult? Update(string id, int player);

    /// <summary>Asks the job running to stop; false when none is.</summary>
    bool CancelJob();
}

/// <summary>
/// A link as staff enter it. <see cref="Token"/> null keeps the saved one, empty removes it;
/// <see cref="Follow"/> is <c>latest</c> or <c>tag</c>.
/// </summary>
public sealed record PluginSourceInput(
    string? Repository,
    string? Follow,
    string? Tag,
    string? Folder,
    string? Token,
    bool AutoUpdate
);

/// <summary>
/// A link, as the panel shows it: never its token, only whether it has one.
/// <see cref="UpdateAvailable"/> when GitHub offers a release other than the one installed.
/// </summary>
public sealed record PluginSourceSnapshot(
    string Id,
    string Repository,
    string Follow,
    string? Tag,
    string? Folder,
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
);

/// <summary>What a link action did: whether it worked, what to tell staff, the link and the job it started.</summary>
public sealed record PluginSourceResult(
    bool Ok,
    string Message,
    PluginSourceSnapshot? Source,
    PluginJobSnapshot? Job
);

public enum PluginJobKind
{
    Check,
    Update,
}

public enum PluginJobStatus
{
    Running,
    Done,
    Failed,
    Canceled,
}

/// <summary>A plugin job: its step (<see cref="Done"/> of <see cref="Total"/>), its log, and how it ended.</summary>
public sealed record PluginJobSnapshot(
    Guid Id,
    PluginJobKind Kind,
    string Title,
    PluginJobStatus Status,
    string Phase,
    int Total,
    int Done,
    ImmutableArray<string> Log,
    string? Result,
    string? Error,
    int PlayerId,
    DateTime StartedAt,
    DateTime? FinishedAt
);
