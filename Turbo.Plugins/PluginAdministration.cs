using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Plugins;
using Turbo.Database.Migrations;
using Turbo.Plugins.Compilation;
using Turbo.Plugins.Data;
using Turbo.Plugins.Exceptions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// <see cref="IPluginAdministration"/> over the <see cref="PluginManager"/>: the same loads,
/// unloads and scans the console's <c>reload-plugin</c> and hot reload use, plus turning a plugin
/// off (<see cref="PluginStateFile"/>) and installing a package into the plugin folder.
/// </summary>
internal sealed partial class PluginAdministration(
    PluginManager manager,
    PluginTables tables,
    IEnumerable<IPluginDataOwner> owners,
    ILogger<PluginAdministration> logger
) : IPluginAdministration
{
    private const string MANIFEST = "manifest.json";

    /// <summary>A plugin's own external texts and variables, given to the hotel as it loads.</summary>
    private const string GAMEDATA = "gamedata";

    /// <summary>
    /// What a data plugin's files may be (<see cref="PluginPackage"/>): data, never a page or a
    /// script a browser would run from the address the plugin's files are served at. No
    /// extension (a <c>LICENSE</c>) is served as a download. A font is data too: a browser loads it
    /// as a face (a theme's <c>text.fonts</c>), never runs it.
    /// </summary>
    private static readonly FrozenSet<string> DATA_FILES = new[]
    {
        "",
        ".json",
        ".txt",
        ".md",
        ".png",
        ".gif",
        ".jpg",
        ".jpeg",
        ".webp",
        ".woff2",
        ".woff",
        ".ttf",
        ".otf",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The first segments core's nodes and meta keys use: never a plugin's, whatever its key.</summary>
    private static readonly FrozenSet<string> CORE_NODE_ROOTS = CoreRoots();

    private readonly IPluginDataOwner[] _owners = [.. owners];

    /// <summary>Held while a package is put in place or a plugin taken away: one at a time.</summary>
    private readonly SemaphoreSlim _changes = new(1, 1);

    /// <summary>Beside the plugins: the earlier versions kept, a zip each. Discovery skips dot-folders.</summary>
    public const string VERSIONS = ".versions";

    public bool UploadEnabled => manager.Config.UploadEnabled;

    public long MaxUploadBytes => manager.Config.MaxUploadBytes;

    public ImmutableArray<PluginSnapshot> List() => manager.Snapshot();

    public PluginContributions? ContributionsOf(string key) => manager.ContributionsOf(key);

    public async Task<PluginActionResult?> ReloadAsync(string key, CancellationToken ct)
    {
        manager.Refresh();

        if (Find(key) is not { } plugin)
            return null;

        if (plugin.State == PluginStateType.Disabled)
            return new PluginActionResult(
                false,
                $"{plugin.Name} is turned off. Turn it on to load it.",
                plugin
            );

        return await RunAsync(key, $"Reloaded {plugin.Name}.", () => manager.ReloadAsync(key, ct))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The scan worked when it ran, whatever the plugins did: its message says, plugin by plugin,
    /// what it loaded and took away, and which plugins failed and why.
    /// </summary>
    public async Task<PluginActionResult> RescanAsync(CancellationToken ct)
    {
        var before = manager.Snapshot().ToDictionary(p => p.Folder, StringComparer.Ordinal);

        await manager.LoadChangedAsync(ct).ConfigureAwait(false);

        var plugins = manager.Snapshot();
        var parts = new List<string>();
        var changed = plugins
            .Where(p =>
                p.State == PluginStateType.Loaded
                && (
                    !before.TryGetValue(p.Folder, out var was)
                    || was.State != PluginStateType.Loaded
                    || was.LoadedAtUtc != p.LoadedAtUtc
                )
            )
            .Select(p => $"{p.Key} {p.Version}".TrimEnd())
            .ToList();
        var gone = before
            .Keys.Except(plugins.Select(p => p.Folder), StringComparer.Ordinal)
            .Select(folder => before[folder].Key)
            .ToList();
        var failed = plugins
            .Where(p => p.State is PluginStateType.Failed or PluginStateType.Rejected)
            .ToList();

        if (changed.Count > 0)
            parts.Add($"loaded {string.Join(", ", changed)}");

        if (gone.Count > 0)
            parts.Add($"took away {string.Join(", ", gone)}");

        if (changed.Count == 0 && gone.Count == 0)
            parts.Add("no plugin loaded or taken away");

        var summary =
            $"{plugins.Count(p => p.State == PluginStateType.Loaded)} loaded"
            + (failed.Count > 0 ? $", {failed.Count} failed" : "");
        var failures = string.Concat(
            failed.Select(p =>
                $" {p.Key} {(p.State == PluginStateType.Rejected ? "was refused" : "failed")}: {p.Error ?? "it did not load."}"
            )
        );

        return new PluginActionResult(
            true,
            $"Scanned the plugin folders: {string.Join("; ", parts)} ({summary}).{failures}",
            null
        );
    }

    public async Task<PluginActionResult?> SetEnabledAsync(
        string key,
        bool enabled,
        CancellationToken ct
    )
    {
        manager.Refresh();

        if (Find(key) is not { } plugin)
            return null;

        if (!enabled)
        {
            var dependents = manager.LiveDependentsOf(plugin.Key);

            if (dependents.Count > 0)
                return new PluginActionResult(
                    false,
                    $"{plugin.Name} can't be turned off while {string.Join(", ", dependents)} depend on it. Turn those off first.",
                    plugin
                );

            manager.State.SetDisabled(plugin.Key, true);

            try
            {
                await manager.DisableAsync(plugin.Key, ct).ConfigureAwait(false);
            }
            catch (PluginDependencyException ex)
            {
                // A dependent loaded in between: it stays on, as it was.
                manager.State.SetDisabled(plugin.Key, false);

                return new PluginActionResult(false, ex.Message, Find(plugin.Key));
            }

            return new PluginActionResult(
                true,
                $"Turned {plugin.Name} off. It stays off after a restart. While it is off, clients are not sent the external variables and texts it brought that the hotel has not changed.",
                Find(plugin.Key)
            );
        }

        manager.State.SetDisabled(plugin.Key, false);

        return await RunAsync(
                plugin.Key,
                $"Turned {plugin.Name} on.",
                () => manager.ReloadAsync(plugin.Key, ct)
            )
            .ConfigureAwait(false);
    }

    public Task<PluginActionResult> InstallAsync(
        Stream package,
        Func<PluginPackage, string?> refuse,
        CancellationToken ct
    ) => InstallAsync(package, null, refuse, ct);

    public ImmutableArray<string> VersionsOf(string key)
    {
        var folder = VersionFolder(key);

        if (folder is null || !Directory.Exists(folder))
            return [];

        var installed = Find(key)?.Version;

        return
        [
            .. new DirectoryInfo(folder)
                .EnumerateFiles("*.zip")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => Path.GetFileNameWithoutExtension(f.Name))
                .Where(v => installed is null || v != FileNameOf(installed)),
        ];
    }

    public async Task<PluginActionResult?> RollbackAsync(
        string key,
        string version,
        Func<PluginPackage, string?> refuse,
        CancellationToken ct
    )
    {
        var folder = VersionFolder(key);
        var path = folder is null ? null : Path.Combine(folder, FileNameOf(version) + ".zip");

        if (path is null || !File.Exists(path))
            return null;

        // Read whole first: installing keeps the version it replaces in this same folder.
        using var copy = new MemoryStream(
            await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false)
        );

        var result = await InstallAsync(copy, key, refuse, ct).ConfigureAwait(false);

        if (!result.Ok)
            return result;

        manager.State.RolledBack(key, version);

        return result with
        {
            Message = $"Rolled {result.Plugin?.Name ?? key} back to {version}.",
        };
    }

    public async Task<PluginUninstallPlan?> PlanUninstallAsync(string key, CancellationToken ct)
    {
        manager.Refresh();

        return Find(key) is { } plugin
            ? (await SurveyAsync(plugin, ct).ConfigureAwait(false)).Plan
            : null;
    }

    public async Task<PluginActionResult?> UninstallAsync(
        string key,
        PluginUninstallMode mode,
        PlayerId actor,
        CancellationToken ct
    )
    {
        // Refused rather than waited for: the install running may be this plugin's own update.
        if (!await _changes.WaitAsync(0, ct).ConfigureAwait(false))
            return Find(key) is { } busy
                ? new PluginActionResult(
                    false,
                    $"A plugin is being installed. Uninstall {busy.Name} when that is done.",
                    busy
                )
                : null;

        try
        {
            manager.Refresh();

            if (Find(key) is not { } plugin)
                return null;

            var survey = await SurveyAsync(plugin, ct).ConfigureAwait(false);

            if (survey.Plan.Refusal is { } refusal)
                return new PluginActionResult(false, refusal, plugin);

            if (mode == PluginUninstallMode.DeleteData && !survey.Complete)
                return new PluginActionResult(
                    false,
                    $"Not all of {plugin.Name}'s data could be looked at (see the dry run), so it was not uninstalled.",
                    plugin
                );

            try
            {
                if (!await manager.TryRemoveAsync(plugin.Key, ct).ConfigureAwait(false))
                    return new PluginActionResult(
                        false,
                        $"{plugin.Name} is loading or reloading. Uninstall it when that is done.",
                        plugin
                    );
            }
            catch (PluginDependencyException ex)
            {
                return new PluginActionResult(false, ex.Message, Find(plugin.Key));
            }

            // Its code is gone: what follows runs to its end whatever the caller does.
            if (VersionFolder(plugin.Key) is { } versions && Directory.Exists(versions))
                Directory.Delete(versions, recursive: true);

            manager.State.EditSources(sources =>
                sources.RemoveAll(x =>
                    string.Equals(x.Key, plugin.Key, StringComparison.OrdinalIgnoreCase)
                )
            );
            manager.State.SetDisabled(plugin.Key, false);

            logger.LogWarning(
                "Plugin {Key}@{Version} uninstalled from the admin panel by player {PlayerId} ({Mode})",
                plugin.Key,
                plugin.Version,
                actor.Value,
                mode
            );

            if (mode == PluginUninstallMode.KeepData)
                return new PluginActionResult(
                    true,
                    $"Uninstalled {plugin.Name}. Its data is kept: installing it again picks it up. Until then, clients are not sent the external variables and texts it brought that the hotel has not changed.",
                    null
                );

            var failures = new List<string>();

            foreach (var (owner, items) in survey.Found.Where(x => !x.Items.IsEmpty))
            {
                try
                {
                    await owner
                        .RemoveAsync(survey.Scope, items, actor, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Deleting the data of the plugin {Key} failed", plugin.Key);
                    failures.Add(ex.Message);
                }
            }

            if (failures.Count > 0)
                return new PluginActionResult(
                    false,
                    $"Uninstalled {plugin.Name}, but deleting some of its data failed: {string.Join(" ", failures)}",
                    null
                );

            return new PluginActionResult(
                true,
                survey.Plan.Data.IsEmpty
                    ? $"Uninstalled {plugin.Name}. It had no data to delete."
                    : $"Uninstalled {plugin.Name} and deleted its data: {Summary(survey.Plan.Data)}.",
                null
            );
        }
        finally
        {
            _changes.Release();
        }
    }

    public async Task<bool> ExportDataAsync(string key, Stream output, CancellationToken ct)
    {
        manager.Refresh();

        if (Find(key) is not { } plugin)
            return false;

        await tables.ExportAsync(ScopeOf(plugin.Key), output, ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// What uninstalling the plugin would take away, from each <see cref="IPluginDataOwner"/>;
    /// <see cref="Survey.Complete"/> is false when one of them could not be asked.
    /// </summary>
    private async Task<Survey> SurveyAsync(PluginSnapshot plugin, CancellationToken ct)
    {
        var scope = ScopeOf(plugin.Key);
        var notes = ImmutableArray.CreateBuilder<string>();
        var found = new List<(IPluginDataOwner Owner, ImmutableArray<PluginDataItem> Items)>();
        var complete = true;

        foreach (var owner in _owners)
        {
            try
            {
                found.Add((owner, await owner.FindAsync(scope, ct).ConfigureAwait(false)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Listing the data of the plugin {Key} failed", plugin.Key);
                notes.Add($"Part of its data could not be looked at: {ex.Message}");
                complete = false;
            }
        }

        if (plugin.State != PluginStateType.Loaded)
            notes.Add(
                "It is not loaded, so its settings overridden in the panel can't be listed: turn it on first to have them deleted too."
            );

        if (scope.TablePrefix is { } prefix && !MigrationHelper.IsPlainTablePrefix(prefix))
            notes.Add(
                $"Its table prefix \"{prefix}\" is not only letters, digits and '_', so its tables can't be told apart from the hotel's and none are listed."
            );

        return new Survey(
            new PluginUninstallPlan(
                plugin,
                FilesOf(plugin),
                [
                    .. manager
                        .State.Sources.Where(x =>
                            string.Equals(x.Key, plugin.Key, StringComparison.OrdinalIgnoreCase)
                        )
                        .Select(x => x.Repository),
                ],
                [.. found.SelectMany(x => x.Items)],
                notes.ToImmutable(),
                RefusalOf(plugin)
            ),
            scope,
            found,
            complete
        );
    }

    /// <summary>Why the plugin can't be uninstalled now; null when it can.</summary>
    private string? RefusalOf(PluginSnapshot plugin)
    {
        if (plugin.Dev)
            return $"{plugin.Name} is a development plugin, loaded from its own folder. Take it out of Turbo:Plugin:DevPluginPaths instead.";

        var dependents = manager
            .Manifests()
            .Where(m =>
                !string.Equals(m.Key, plugin.Key, StringComparison.OrdinalIgnoreCase)
                && m.Dependencies.Any(d =>
                    string.Equals(d.Key, plugin.Key, StringComparison.OrdinalIgnoreCase)
                )
            )
            .Select(m => m.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return dependents.Count > 0
            ? $"{string.Join(", ", dependents)} depend on {plugin.Name}. Uninstall those first."
            : null;
    }

    /// <summary>Its folder and the earlier versions kept of it, as the plugin folder names them.</summary>
    private ImmutableArray<string> FilesOf(PluginSnapshot plugin)
    {
        var versions = VersionFolder(plugin.Key);
        var kept =
            versions is not null && Directory.Exists(versions)
                ? new DirectoryInfo(versions)
                    .EnumerateFiles("*.zip")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Select(f => $"{VERSIONS}/{plugin.Key}/{f.Name}")
                : [];

        return [plugin.Folder + "/", .. kept];
    }

    /// <summary>
    /// Whose data is the plugin's: its table prefix, those of the other plugins known, and the
    /// roots of its nodes, <c>plugin.&lt;key&gt;</c> and its key, unless core uses that root.
    /// </summary>
    private PluginDataScope ScopeOf(string key)
    {
        var manifests = manager.Manifests();
        var manifest = manifests.FirstOrDefault(m =>
            string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase)
        );
        var root = key.ToLowerInvariant();
        var roots = ImmutableArray.Create($"{PluginAdminRoutes.NODE_ROOT}.{root}");

        if (
            PermissionNodeFormat.IsValidSegment(root)
            && root != PluginAdminRoutes.NODE_ROOT
            && !CORE_NODE_ROOTS.Contains(root)
        )
            roots = roots.Add(root);

        return new PluginDataScope(
            manifest?.Key ?? key,
            manifest?.TablePrefix,
            [
                .. manifests
                    .Where(m => !ReferenceEquals(m, manifest))
                    .Select(m => m.TablePrefix)
                    .OfType<string>(),
            ],
            roots,
            manager.FolderOf(key)
        );
    }

    private static FrozenSet<string> CoreRoots()
    {
        var core = new CorePermissionNodeSource();

        return core
            .Nodes.Select(x => x.Node)
            .Concat(core.MetaKeys.Select(x => x.Key))
            .Select(PermissionNodeFormat.RootOf)
            .ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>"2 tables (14 rows), 1 setting, 3 grants", and ", 2 variables, 5 texts" when it brought any.</summary>
    private static string Summary(ImmutableArray<PluginDataItem> data)
    {
        string Count(PluginDataKind kind, string one, string many)
        {
            var n = data.Count(x => x.Kind == kind);

            return n == 1 ? $"1 {one}" : $"{n} {many}";
        }

        var rows = data.Where(x => x.Kind == PluginDataKind.Table).Sum(x => x.Count);

        var summary =
            $"{Count(PluginDataKind.Table, "table", "tables")} ({rows} rows), {Count(PluginDataKind.Setting, "setting", "settings")}, {Count(PluginDataKind.Grant, "grant", "grants")}";

        // What it brought of the client's gamedata, only when it brought any.
        if (data.Any(x => x.Kind is PluginDataKind.Variable or PluginDataKind.Text))
            summary +=
                $", {Count(PluginDataKind.Variable, "variable", "variables")}, {Count(PluginDataKind.Text, "text", "texts")}";

        return summary;
    }

    /// <summary>
    /// <see cref="InstallAsync(Stream, Func{PluginPackage, string?}, CancellationToken)"/>; with
    /// <paramref name="expectedKey"/>, a package holding another plugin is refused. The version
    /// replaced is kept (see <see cref="VersionsOf"/>), and put back when the new one does not load.
    /// </summary>
    internal async Task<PluginActionResult> InstallAsync(
        Stream package,
        string? expectedKey,
        Func<PluginPackage, string?> refuse,
        CancellationToken ct
    )
    {
        if (!UploadEnabled)
            return new PluginActionResult(false, "Installing plugins from the panel is off.", null);

        await _changes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await InstallHeldAsync(package, expectedKey, refuse, ct).ConfigureAwait(false);
        }
        finally
        {
            _changes.Release();
        }
    }

    private async Task<PluginActionResult> InstallHeldAsync(
        Stream package,
        string? expectedKey,
        Func<PluginPackage, string?> refuse,
        CancellationToken ct
    )
    {
        var staging = Path.Combine(
            Path.GetTempPath(),
            "turbo-plugin-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            if (await UnpackAsync(package, staging, ct).ConfigureAwait(false) is { } refusal)
                return new PluginActionResult(false, refusal, null);

            PluginManifest manifest;

            try
            {
                manifest = PluginHelpers.ReadManifest(staging);
            }
            catch (PluginException ex)
            {
                return new PluginActionResult(false, Describe(ex), null);
            }

            // The server compiles a plugin from its sources: a package with an executable file
            // in it would be refused as it loads, so it is not installed.
            if (PluginFolderGate.Find(staging) is { Count: > 0 } executables)
                return new PluginActionResult(
                    false,
                    $"The package holds {string.Join(", ", executables)}: a plugin is installed as its C# sources and the server compiles them, so a package has no executable file.",
                    null
                );

            if (!KeyPattern().IsMatch(manifest.Key))
                return new PluginActionResult(
                    false,
                    $"The manifest's Key \"{manifest.Key}\" can only have letters, digits, '.', '_' and '-'.",
                    null
                );

            if (
                expectedKey is not null
                && !string.Equals(manifest.Key, expectedKey, StringComparison.OrdinalIgnoreCase)
            )
                return new PluginActionResult(
                    false,
                    $"The package holds the plugin \"{manifest.Key}\", not \"{expectedKey}\".",
                    null
                );

            if (PluginApi.Refusal(manifest) is { } apiRefusal)
                return new PluginActionResult(false, apiRefusal, null);

            using var publication = manager.TryReservePublication(manifest.Key);
            if (publication is null)
                return new PluginActionResult(
                    false,
                    $"The plugin key {manifest.Key} is already being published: try again later.",
                    null
                );

            manager.Refresh();

            var existing = manager.FolderOf(manifest.Key);

            if (existing is not null && manager.IsDev(existing))
                return new PluginActionResult(
                    false,
                    $"A development plugin with the key \"{manifest.Key}\" is loaded from {existing}; it can't be replaced from the panel.",
                    null
                );

            if (refuse(PackageOf(manifest, staging, existing)) is { } refused)
                return new PluginActionResult(false, refused, null);

            var target = existing ?? Path.Combine(manager.Config.PluginFolderPath, manifest.Key);
            var kept = existing is null ? null : Keep(manifest.Key, existing);

            Replace(staging, target);

            logger.LogWarning(
                "Plugin {Name}@{Version} by {Author} installed into {Folder} from the admin panel",
                manifest.Name,
                manifest.Version,
                manifest.Author,
                target
            );

            manager.Refresh();

            if (Find(manifest.Key) is { State: PluginStateType.Disabled } off)
                return new PluginActionResult(
                    true,
                    $"Installed {manifest.Name} {manifest.Version}. It is turned off: turn it on to load it.",
                    off
                );

            var loaded = await RunAsync(
                    manifest.Key,
                    $"Installed and loaded {manifest.Name} {manifest.Version}.",
                    () => manager.ReloadAsync(manifest.Key, ct)
                )
                .ConfigureAwait(false);

            if (loaded.Ok || kept is null)
                return loaded;

            return await PutBackAsync(manifest, kept.Value, target, loaded.Message, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Could not remove the staging folder {Folder}", staging);
            }
        }
    }

    /// <summary>
    /// What the unpacked package in <paramref name="staging"/> holds: the file that makes it code,
    /// if one does, whether the plugin it replaces in <paramref name="existing"/> has code (one
    /// whose manifest can't be read is taken to), and whether it brings gamedata.
    /// </summary>
    private static PluginPackage PackageOf(
        PluginManifest manifest,
        string staging,
        string? existing
    )
    {
        var codeFile = PluginHelpers.IsDataOnly(manifest, staging)
            ? Directory
                .EnumerateFiles(staging, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(staging, file).Replace('\\', '/'))
                .Where(file => !DATA_FILES.Contains(Path.GetExtension(file)))
                .Order(StringComparer.Ordinal)
                .FirstOrDefault()
            : SourceFileOf(manifest, staging);
        var replacesCode = false;

        if (existing is not null)
            try
            {
                replacesCode = !PluginHelpers.IsDataOnly(
                    PluginHelpers.ReadManifest(existing),
                    existing
                );
            }
            catch (PluginException)
            {
                replacesCode = true;
            }

        var gamedata = Path.Combine(staging, GAMEDATA);

        return new PluginPackage(
            manifest.Key,
            manifest.Name,
            manifest.Version,
            codeFile,
            replacesCode,
            Directory.Exists(gamedata) && Directory.EnumerateFiles(gamedata).Any()
        );
    }

    /// <summary>The first of the plugin's source files: what makes the package code.</summary>
    private static string SourceFileOf(PluginManifest manifest, string staging)
    {
        try
        {
            // A client-only plugin, or code its Sources miss (which the server refuses), has none.
            return PluginSourceFiles.Find(staging, manifest).FirstOrDefault()
                ?? PluginSourceFiles.CodeFiles(staging, "*.cs").FirstOrDefault()
                ?? PluginSourceFiles.CodeFiles(staging, "*.csproj").FirstOrDefault()
                ?? "manifest.json";
        }
        catch (ArgumentException)
        {
            return "manifest.json";
        }
    }

    /// <summary>
    /// The version that did not load is taken out again and the one it replaced installed; what
    /// the operator sees is why the new one failed, and that the old one is back.
    /// </summary>
    private async Task<PluginActionResult> PutBackAsync(
        PluginManifest failed,
        (string Version, string Path) kept,
        string target,
        string error,
        CancellationToken ct
    )
    {
        var staging = Path.Combine(
            Path.GetTempPath(),
            "turbo-plugin-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            await ZipFile.ExtractToDirectoryAsync(kept.Path, staging, ct).ConfigureAwait(false);
            Replace(staging, target);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }

        logger.LogWarning(
            "Plugin {Key}@{Version} did not load; put back {Kept}",
            failed.Key,
            failed.Version,
            kept.Version
        );

        manager.Refresh();

        var back = await RunAsync(failed.Key, "", () => manager.ReloadAsync(failed.Key, ct))
            .ConfigureAwait(false);

        return new PluginActionResult(
            false,
            back.Ok
                ? $"{failed.Name} {failed.Version} did not load: {error} Put back {kept.Version}."
                : $"{failed.Name} {failed.Version} did not load: {error} Putting back {kept.Version} failed too: {back.Message}",
            back.Plugin
        );
    }

    /// <summary>
    /// Zips the plugin's folder as it is into <c>.versions/&lt;key&gt;/&lt;version&gt;.zip</c>
    /// and drops the oldest past <c>KeepVersions</c>; null when its manifest can't be read.
    /// </summary>
    private (string Version, string Path)? Keep(string key, string folder)
    {
        string version;

        try
        {
            version = PluginHelpers.ReadManifest(folder).Version;
        }
        catch (PluginException)
        {
            return null;
        }

        var versions = VersionFolder(key)!;
        var path = Path.Combine(versions, FileNameOf(version) + ".zip");

        Directory.CreateDirectory(versions);

        if (File.Exists(path))
            File.Delete(path);

        ZipFile.CreateFromDirectory(folder, path);

        foreach (
            var old in new DirectoryInfo(versions)
                .EnumerateFiles("*.zip")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(Math.Max(1, manager.Config.KeepVersions))
        )
            old.Delete();

        return (version, path);
    }

    private string? VersionFolder(string key) =>
        KeyPattern().IsMatch(key)
            ? Path.Combine(manager.Config.PluginFolderPath, VERSIONS, key)
            : null;

    /// <summary>The version as a file name: anything a path can't hold becomes '_'.</summary>
    private static string FileNameOf(string version) =>
        string.Concat(
                version.Select(c =>
                    char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+' ? c : '_'
                )
            )
            .TrimStart('.');

    private PluginSnapshot? Find(string key) =>
        manager
            .Snapshot()
            .FirstOrDefault(p =>
                p.Name is not null && string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)
            );

    private async Task<PluginActionResult> RunAsync(string key, string done, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Plugin action on {Key} failed", key);

            return new PluginActionResult(false, Describe(ex), Find(key));
        }

        var plugin = Find(key);

        return plugin is { State: PluginStateType.Loaded }
            ? new PluginActionResult(true, done, plugin)
            : new PluginActionResult(false, plugin?.Error ?? "The plugin did not load.", plugin);
    }

    /// <summary>
    /// Unpacks the zip into <paramref name="staging"/>; a reason to refuse it, or null. The
    /// manifest and assembly are at its root, or in its one top folder (a zipped plugin folder).
    /// </summary>
    private async Task<string?> UnpackAsync(Stream package, string staging, CancellationToken ct)
    {
        var limit = MaxUploadBytes;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await package.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
                return $"The package is larger than {limit / (1024 * 1024)} MB.";

            await buffer.WriteAsync(chunk.AsMemory(0, read), ct).ConfigureAwait(false);
        }

        buffer.Position = 0;

        ZipArchive zip;

        try
        {
            zip = new ZipArchive(buffer, ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            return "The package isn't a zip file.";
        }

        using (zip)
        {
            var files = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();

            if (files.Sum(e => e.Length) > limit)
                return $"The package unpacks to more than {limit / (1024 * 1024)} MB.";

            var root = RootOf(files);

            if (root is null)
                return "The package has no manifest.json at its root.";

            Directory.CreateDirectory(staging);

            var full = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;

            foreach (var entry in files)
            {
                var relative = entry.FullName.Replace('\\', '/')[root.Length..];
                var path = Path.GetFullPath(Path.Combine(staging, relative));

                // A name that climbs out of the folder ("../") is refused, whatever else is in it.
                if (!path.StartsWith(full, StringComparison.OrdinalIgnoreCase))
                    return $"The package has a file outside its folder: {entry.FullName}.";

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var source = await entry.OpenAsync(ct).ConfigureAwait(false);
                await using var sourceScope = source.ConfigureAwait(false);
                var file = File.Create(path);
                await using var fileScope = file.ConfigureAwait(false);
                await source.CopyToAsync(file, ct).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <summary>"" when manifest.json is at the zip's root, "top/" when it is in its one top folder.</summary>
    private static string? RootOf(List<ZipArchiveEntry> files)
    {
        var names = files.Select(e => e.FullName.Replace('\\', '/')).ToList();

        if (names.Contains(MANIFEST, StringComparer.OrdinalIgnoreCase))
            return string.Empty;

        var tops = names.Select(n => n.Split('/')[0]).Distinct().ToList();

        return
            tops.Count == 1
            && names.Contains($"{tops[0]}/{MANIFEST}", StringComparer.OrdinalIgnoreCase)
            ? tops[0] + "/"
            : null;
    }

    /// <summary>
    /// Puts the staged files in place of the plugin's folder. Its plugin is loaded from memory,
    /// so nothing holds its files.
    /// </summary>
    private static void Replace(string staging, string target)
    {
        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);

        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(staging, file));

            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to, overwrite: true);
        }
    }

    /// <summary>What went wrong, without the server's own paths.</summary>
    private static string Describe(Exception ex) =>
        ex switch
        {
            PluginManifestException { ErrorType: PluginManifestErrorType.NotFound } =>
                "The package has no manifest.json.",
            PluginManifestException { ErrorType: PluginManifestErrorType.MissingField } m =>
                $"The manifest has no {m.FieldName}.",
            PluginManifestException => "The manifest isn't valid JSON for a plugin manifest.",
            _ => ex.Message,
        };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex KeyPattern();

    /// <summary>A dry run, with what each owner answered, for the uninstall that follows it.</summary>
    private sealed record Survey(
        PluginUninstallPlan Plan,
        PluginDataScope Scope,
        List<(IPluginDataOwner Owner, ImmutableArray<PluginDataItem> Items)> Found,
        bool Complete
    );
}
