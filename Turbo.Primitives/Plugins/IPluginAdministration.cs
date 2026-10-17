using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// The plugins this server knows of, for the admin panel: what each is, whether it loaded and
/// what it added, and the operator's hand on them - reload one, scan the folder again, turn one
/// off or on (remembered across restarts) and install a package. Who may do which is the
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

    /// <summary>Loads the plugin again from its folder. Null when no plugin has the key.</summary>
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
}

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
    DateTime? LoadedAtUtc
);

/// <summary>What an action did: whether it worked, what to tell the operator, and the plugin after it.</summary>
public sealed record PluginActionResult(bool Ok, string Message, PluginSnapshot? Plugin);
