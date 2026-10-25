using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Policies;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// The plugins this server knows of, for the admin panel: what each is, whether it loaded and
/// what it added, and the operator's hand on them - reload one, scan the folder again, turn one
/// off or on (remembered across restarts), install a package and uninstall one. Who may do which is the
/// caller's to check; every change here is the server's whole plugin set.
/// </summary>
public interface IPluginAdministration
{
    /// <summary>
    /// Whether <see cref="InstallAsync"/> is allowed at all (<c>Turbo:Plugin:UploadEnabled</c>).
    /// Off unless the operator turns it on: a package is server code.
    /// </summary>
    public bool UploadEnabled { get; }

    /// <summary>The largest package <see cref="InstallAsync"/> takes, in bytes.</summary>
    public long MaxUploadBytes { get; }

    /// <summary>Every plugin found on disk, and every folder whose manifest could not be read.</summary>
    public ImmutableArray<PluginSnapshot> List();

    /// <summary>
    /// Everything the plugin contributes, for the extension inspector: what its last load
    /// registered (a failed one: up to where it failed), clashes with other live plugins, and what
    /// clients report of its client half. Null when no plugin has the key.
    /// </summary>
    public PluginContributions? ContributionsOf(string key);

    /// <summary>
    /// Loads the plugin again from its folder, then the plugins that depend on it; the others keep
    /// running. Null when no plugin has the key.
    /// </summary>
    public Task<PluginActionResult?> ReloadAsync(string key, CancellationToken ct);

    /// <summary>Scans the plugin folders again: loads new and changed plugins, unloads removed ones.</summary>
    public Task<PluginActionResult> RescanAsync(CancellationToken ct);

    /// <summary>
    /// Turns the plugin on or off, and remembers it across restarts. Off unloads it (refused
    /// while a plugin that depends on it is loaded); on loads it. Null when no plugin has the key.
    /// </summary>
    public Task<PluginActionResult?> SetEnabledAsync(
        string key,
        bool enabled,
        CancellationToken ct
    );

    /// <summary>
    /// Installs a plugin package: a zip with <c>manifest.json</c> and the plugin's assembly at
    /// its root. The manifest is checked before anything on disk changes; an installed plugin
    /// with the same key is replaced, and the plugin is loaded.
    /// </summary>
    public Task<PluginActionResult> InstallAsync(Stream package, CancellationToken ct);

    /// <summary>
    /// The earlier versions of the plugin kept to roll back to (<c>Turbo:Plugin:KeepVersions</c>),
    /// newest first, without the one installed.
    /// </summary>
    public ImmutableArray<string> VersionsOf(string key);

    /// <summary>
    /// Installs a kept earlier version again, as a package is installed; the version it replaces
    /// is kept in turn. Null when the plugin has no such version kept.
    /// </summary>
    public Task<PluginActionResult?> RollbackAsync(
        string key,
        string version,
        CancellationToken ct
    );

    /// <summary>
    /// The dry run of <see cref="UninstallAsync"/>: what uninstalling the plugin would take away,
    /// and why it can't be uninstalled now, if it can't. Changes nothing. Null when no plugin has
    /// the key.
    /// </summary>
    public Task<PluginUninstallPlan?> PlanUninstallAsync(string key, CancellationToken ct);

    /// <summary>
    /// Unloads the plugin, then deletes its folder, the earlier versions kept of it and the GitHub
    /// links it was installed from. With <see cref="PluginUninstallMode.DeleteData"/> its data goes
    /// too (<see cref="IPluginDataOwner"/>); kept, a reinstall picks it up again. Refused while
    /// the plugin is loading, an install is running, another installed plugin depends on it, or it
    /// is a development plugin. Null when no plugin has the key.
    /// </summary>
    public Task<PluginActionResult?> UninstallAsync(
        string key,
        PluginUninstallMode mode,
        PlayerId actor,
        CancellationToken ct
    );

    /// <summary>
    /// Writes the plugin's tables, their definitions and rows, as SQL to <paramref name="output"/>,
    /// to keep before deleting its data. False when no plugin has the key.
    /// </summary>
    public Task<bool> ExportDataAsync(string key, Stream output, CancellationToken ct);
}

/// <summary>What uninstalling a plugin takes away besides its code.</summary>
public enum PluginUninstallMode
{
    /// <summary>Its folder, kept versions and links; its tables, settings and grants stay for a reinstall.</summary>
    KeepData,

    /// <summary>Everything <see cref="KeepData"/> does, and its tables, settings and grants.</summary>
    DeleteData,
}

/// <summary>
/// What uninstalling <see cref="Plugin"/> would take away: <see cref="Files"/> (its folder and the
/// earlier versions kept, as the plugin folder names them), <see cref="Sources"/> (the GitHub
/// repositories linked to it) and, with its data, <see cref="Data"/>. <see cref="Notes"/> say what
/// could not be looked at; <see cref="Refusal"/> is why it can't be uninstalled now, null when it can.
/// </summary>
public sealed record PluginUninstallPlan(
    PluginSnapshot Plugin,
    ImmutableArray<string> Files,
    ImmutableArray<string> Sources,
    ImmutableArray<PluginDataItem> Data,
    ImmutableArray<string> Notes,
    string? Refusal
);

/// <summary>How a plugin stands right now.</summary>
public enum PluginStateType
{
    /// <summary>Running.</summary>
    Loaded,

    /// <summary>Its last load failed; <see cref="PluginSnapshot.Error"/> says why.</summary>
    Failed,

    /// <summary>Turned off by the operator; not loaded until turned on.</summary>
    Disabled,

    /// <summary>On disk and not loaded yet (found after the last scan).</summary>
    NotLoaded,
}

/// <summary>One plugin, as the admin panel shows it.</summary>
/// <param name="Key">The manifest's key; for a folder whose manifest is unreadable, the folder's name.</param>
/// <param name="Folder">The folder's name (dev plugins: its full path).</param>
/// <param name="Error">Why the manifest could not be read, or why its last load failed.</param>
/// <param name="Capabilities">The client capabilities it registered.</param>
/// <param name="IncomingHeaders">The headers of the incoming packets it reads.</param>
/// <param name="OutgoingHeaders">The headers of the outgoing packets it writes.</param>
/// <param name="Migrations">The database migrations applied for its tables, oldest first.</param>
/// <param name="Policies">
/// The game policies it registered; one whose <c>SharedWith</c> is not empty conflicts with
/// another plugin's of the same kind (both run, in order).
/// </param>
public sealed record PluginSnapshot(
    string Key,
    string? Name,
    string? Version,
    string? Author,
    string Folder,
    bool Dev,
    PluginStateType State,
    string? Error,
    ImmutableArray<string> Dependencies,
    ImmutableArray<string> Capabilities,
    ImmutableArray<int> IncomingHeaders,
    ImmutableArray<int> OutgoingHeaders,
    ImmutableArray<string> Migrations,
    DateTime? LoadedAtUtc,
    ImmutableArray<RegisteredPolicy> Policies = default
);

/// <summary>What an action did: whether it worked, what to tell the operator, and the plugin after it.</summary>
public sealed record PluginActionResult(bool Ok, string Message, PluginSnapshot? Plugin);
