using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Exceptions;

namespace Turbo.Plugins;

/// <summary>
/// Reloads plugins as their files change on disk: a code file (<see cref="PluginHelpers.IsCodeFile"/>)
/// loads what changed (<see cref="PluginManager.LoadChangedAsync"/>); any other file of a data plugin
/// (<see cref="PluginHelpers.IsDataOnly"/>: its theme, patches, images, texts) reloads that plugin
/// (<see cref="PluginManager.ReloadAsync"/>), so a theme author's edit in their editor reaches the
/// players drawing it with no reload of theirs (live reload, <c>docs/plugin-data.md</c>). A burst of
/// changes is one reload, <see cref="PluginConfig.DebounceMs"/> after the last.
/// </summary>
public sealed class PluginHotReloadService(
    PluginManager pluginManager,
    IOptions<PluginConfig> config,
    ILogger<PluginHotReloadService> logger
) : IHostedService, IDisposable
{
    private readonly PluginManager _pluginManager = pluginManager;
    private readonly PluginConfig _config = config.Value;
    private readonly ILogger<PluginHotReloadService> _logger = logger;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly Lock _stateLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly List<FileSystemWatcher> _watchers = [];

    private Timer? _debounceTimer;
    private Timer? _dataTimer;
    private readonly HashSet<string> _pendingData = new(StringComparer.OrdinalIgnoreCase);
    private bool _pendingReload;
    private string? _lastPath;
    private int _shutdownStarted;

    public Task StartAsync(CancellationToken ct)
    {
        _debounceTimer = new Timer(
            _ => _ = Task.Run(ProcessReloadAsync, _cts.Token),
            state: null,
            Timeout.Infinite,
            Timeout.Infinite
        );

        _dataTimer = new Timer(
            _ => _ = Task.Run(ProcessDataReloadAsync, _cts.Token),
            state: null,
            Timeout.Infinite,
            Timeout.Infinite
        );

        WatchDirectory(_config.PluginFolderPath, includeSubdirectories: true);

        // A dev plugin's data files are in its folders (wwwroot/, gamedata/).
        foreach (var devPath in _config.DevPluginPaths)
            WatchDirectory(Path.GetFullPath(devPath), includeSubdirectories: true);

        _logger.LogInformation("Plugin hot reload is enabled.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        Shutdown();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Shutdown();
        _reloadGate.Dispose();
        _cts.Dispose();
    }

    private void WatchDirectory(string path, bool includeSubdirectories)
    {
        if (!Directory.Exists(path))
            return;

        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter =
                NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.CreationTime
                | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        watcher.Changed += OnFsEvent;
        watcher.Created += OnFsEvent;
        watcher.Deleted += OnFsEvent;
        watcher.Renamed += OnFsRenamed;
        watcher.Error += OnFsError;

        _watchers.Add(watcher);

        _logger.LogDebug("Watching plugin path: {Path}", path);
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        if (Volatile.Read(ref _shutdownStarted) == 1)
            return;

        if (!ShouldReactToPath(e.FullPath))
        {
            QueueDataReload(e.FullPath);

            return;
        }

        QueueDebouncedReload(e.FullPath, e.ChangeType.ToString());
    }

    private void OnFsRenamed(object sender, RenamedEventArgs e)
    {
        if (Volatile.Read(ref _shutdownStarted) == 1)
            return;

        if (!ShouldReactToPath(e.FullPath) && !ShouldReactToPath(e.OldFullPath))
        {
            QueueDataReload(e.FullPath);

            return;
        }

        QueueDebouncedReload(e.FullPath, "Renamed");
    }

    private void OnFsError(object sender, ErrorEventArgs e)
    {
        _logger.LogWarning(e.GetException(), "Plugin file watcher emitted an error event.");
    }

    private void QueueDebouncedReload(string fullPath, string changeType)
    {
        lock (_stateLock)
        {
            _pendingReload = true;
            _lastPath = fullPath;
            _debounceTimer?.Change(Math.Max(100, _config.DebounceMs), Timeout.Infinite);
        }

        _logger.LogDebug(
            "Plugin change detected ({Type}) at {Path}; reload scheduled.",
            changeType,
            fullPath
        );
    }

    /// <summary>
    /// The folder of the plugin a file that is no code file belongs to, when it is a data plugin's:
    /// a dev plugin's folder (<see cref="PluginConfig.DevPluginPaths"/>) or a folder of
    /// <see cref="PluginConfig.PluginFolderPath"/>, whose manifest names no assembly. Null for a
    /// file of the plugins folder itself, of a plugin with code, or of a folder with no manifest.
    /// </summary>
    public static string? DataPluginFolderOf(
        string path,
        string pluginFolderPath,
        IEnumerable<string> devPluginPaths
    )
    {
        var full = Path.GetFullPath(path);
        string? folder = null;

        foreach (var dev in devPluginPaths.Select(Path.GetFullPath))
            if (IsUnder(full, dev))
                folder = dev;

        if (folder is null && IsUnder(full, Path.GetFullPath(pluginFolderPath)))
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(pluginFolderPath), full);
            var first = relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                2
            );

            if (first.Length == 2)
                folder = Path.Combine(Path.GetFullPath(pluginFolderPath), first[0]);
        }

        if (folder is null || !File.Exists(Path.Combine(folder, "manifest.json")))
            return null;

        try
        {
            return PluginHelpers.IsDataOnly(PluginHelpers.ReadManifest(folder)) ? folder : null;
        }
        catch (Exception ex)
            when (ex is PluginManifestException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsUnder(string path, string folder) =>
        path.StartsWith(
            Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        );

    private void QueueDataReload(string fullPath)
    {
        string? folder;

        try
        {
            folder = DataPluginFolderOf(fullPath, _config.PluginFolderPath, _config.DevPluginPaths);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return;
        }

        if (folder is null)
            return;

        lock (_stateLock)
        {
            _pendingData.Add(folder);
            _dataTimer?.Change(Math.Max(100, _config.DebounceMs), Timeout.Infinite);
        }
    }

    /// <summary>Reloads each data plugin whose files changed, one after another; one that fails is logged.</summary>
    private async Task ProcessDataReloadAsync()
    {
        if (_cts.IsCancellationRequested)
            return;

        List<string> folders;

        lock (_stateLock)
        {
            folders = [.. _pendingData];
            _pendingData.Clear();
        }

        foreach (var folder in folders)
        {
            try
            {
                var key = PluginHelpers.ReadManifest(folder).Key;

                await _pluginManager.ReloadAsync(key, _cts.Token).ConfigureAwait(false);
                _logger.LogInformation("Data plugin {Key} reloaded: its files changed", key);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reloading the data plugin in {Folder} failed", folder);
            }
        }
    }

    private async Task ProcessReloadAsync()
    {
        if (_cts.IsCancellationRequested)
            return;

        string? changedPath;

        lock (_stateLock)
        {
            if (!_pendingReload)
                return;

            _pendingReload = false;
            changedPath = _lastPath;
        }

        try
        {
            await _reloadGate.WaitAsync(_cts.Token).ConfigureAwait(false);

            _logger.LogInformation(
                "Plugin reload started (trigger: {Path})",
                changedPath ?? "<unknown>"
            );

            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await _pluginManager.LoadChangedAsync(_cts.Token).ConfigureAwait(false);
                    _logger.LogInformation("Plugin reload completed.");
                    return;
                }
                catch (Exception ex)
                    when (attempt < maxAttempts && ex is IOException or InvalidDataException)
                {
                    _logger.LogWarning(
                        ex,
                        "Plugin reload attempt {Attempt}/{MaxAttempts} failed due to file churn; retrying.",
                        attempt,
                        maxAttempts
                    );

                    await Task.Delay(250, _cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // host is stopping
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin reload failed.");
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private static bool ShouldReactToPath(string path) => PluginHelpers.IsCodeFile(path);

    private void DisposeWatchers()
    {
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Changed -= OnFsEvent;
            w.Created -= OnFsEvent;
            w.Deleted -= OnFsEvent;
            w.Renamed -= OnFsRenamed;
            w.Error -= OnFsError;
            w.Dispose();
        }

        _watchers.Clear();
    }

    private void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
            return;

        DisposeWatchers();
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _dataTimer?.Dispose();
        _dataTimer = null;
        _cts.Cancel();
    }
}
