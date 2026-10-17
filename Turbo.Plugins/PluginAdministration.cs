using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Plugins;
using Turbo.Plugins.Exceptions;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// <see cref="IPluginAdministration"/> over the <see cref="PluginManager"/>: the same loads,
/// unloads and scans the console's <c>reload-plugin</c> and hot reload use, plus turning a plugin
/// off (<see cref="PluginStateFile"/>) and installing a package into the plugin folder.
/// </summary>
internal sealed partial class PluginAdministration(
    PluginManager manager,
    ILogger<PluginAdministration> logger
) : IPluginAdministration
{
    private const string MANIFEST = "manifest.json";

    public bool UploadEnabled => manager.Config.UploadEnabled;

    public long MaxUploadBytes => manager.Config.MaxUploadBytes;

    public ImmutableArray<PluginSnapshot> List() => manager.Snapshot();

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

    public async Task<PluginActionResult> RescanAsync(CancellationToken ct)
    {
        await manager.LoadAllAsync(true, ct).ConfigureAwait(false);

        var plugins = manager.Snapshot();
        var loaded = plugins.Count(p => p.State == PluginStateType.Loaded);
        var failed = plugins.Count(p => p.State == PluginStateType.Failed);

        return new PluginActionResult(
            failed == 0,
            failed == 0
                ? $"Scanned the plugin folders: {loaded} loaded."
                : $"Scanned the plugin folders: {loaded} loaded, {failed} failed.",
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
                $"Turned {plugin.Name} off. It stays off after a restart.",
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

    public async Task<PluginActionResult> InstallAsync(Stream package, CancellationToken ct)
    {
        if (!UploadEnabled)
            return new PluginActionResult(false, "Installing plugins from the panel is off.", null);

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
                PluginHelpers.GetAssemblyPath(staging, manifest);
            }
            catch (PluginException ex)
            {
                return new PluginActionResult(false, Describe(ex), null);
            }

            if (!KeyPattern().IsMatch(manifest.Key))
                return new PluginActionResult(
                    false,
                    $"The manifest's Key \"{manifest.Key}\" can only have letters, digits, '.', '_' and '-'.",
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

            var target = existing ?? Path.Combine(manager.Config.PluginFolderPath, manifest.Key);

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

            return await RunAsync(
                    manifest.Key,
                    $"Installed and loaded {manifest.Name} {manifest.Version}.",
                    () => manager.ReloadAsync(manifest.Key, ct)
                )
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

            buffer.Write(chunk, 0, read);
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
                await using var source = await entry.OpenAsync(ct).ConfigureAwait(false);
                await using var file = File.Create(path);
                await source.CopyToAsync(file, ct).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <summary>"" when manifest.json is at the zip's root, "top/" when it is in its one top folder.</summary>
    private static string? RootOf(System.Collections.Generic.List<ZipArchiveEntry> files)
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
            PluginAssemblyException { ErrorType: PluginAssemblyErrorType.NotFound } =>
                "The package has no assembly named as the manifest's AssemblyFile.",
            _ => ex.Message,
        };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex KeyPattern();
}
