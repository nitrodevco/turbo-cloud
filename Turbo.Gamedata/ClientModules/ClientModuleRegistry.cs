using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Api;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.ClientModules;

namespace Turbo.Gamedata.ClientModules;

/// <summary>
/// The client modules the hotel's plugins brought (<see cref="IClientModuleDirectory"/>). Each is
/// served from its plugin's <c>wwwroot/</c> by the gamedata host, under
/// <see cref="GamedataServer.PLUGIN_FILES_ROUTE"/>; its address names its entry file's SHA-256
/// (<c>?v=</c>), so a browser keeps a version for good and loads the next one when it changes. The
/// address starts with <see cref="GamedataConfig.PublicUrl"/> when the hotel set one, and is
/// site-relative otherwise. A registry reached outside a plugin has no folder to serve from and
/// refuses <see cref="Register"/>: plugins are given one bound to their own folder.
/// </summary>
internal sealed class ClientModuleRegistry(
    IOptionsMonitor<GamedataConfig> config,
    ILogger<ClientModuleRegistry> logger
) : IClientModuleDirectory, IClientModuleRegistry
{
    /// <summary>How much of the hash the address carries: enough to tell versions apart.</summary>
    public const int VERSION_LENGTH = 16;

    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<
        string,
        (long Length, DateTime Written, string Hash)
    > _hashes = new(StringComparer.OrdinalIgnoreCase);
    private ImmutableSortedDictionary<string, ClientModuleSource> _modules =
        ImmutableSortedDictionary.Create<string, ClientModuleSource>(
            StringComparer.OrdinalIgnoreCase
        );

    public event Action? Changed;

    public ImmutableArray<ClientModuleSnapshot> Modules =>
        [.. _modules.Values.Select(Snapshot).OfType<ClientModuleSnapshot>()];

    public IDisposable Register(string key, string relativePath) =>
        throw new InvalidOperationException(
            "Only a plugin can register a client module, through the registry its host services give it: it is served from the plugin's own folder."
        );

    public IDisposable Add(ClientModuleSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.Key))
            throw new ArgumentException("A client module needs its plugin's key.", nameof(source));

        var root = Path.GetFullPath(source.Root);

        if (Resolve(root, source.Entry) is null)
            throw new ArgumentException(
                $"The client module {source.Key}'s entry file {source.Entry} is not a file in its folder.",
                nameof(source)
            );

        source = source with { Root = root, Entry = Normalize(source.Entry) };

        lock (_lock)
        {
            if (_modules.ContainsKey(source.Key))
                throw new InvalidOperationException(
                    $"The plugin {source.Key} already has a client module."
                );

            _modules = _modules.Add(source.Key, source);
        }

        var options = config.CurrentValue;

        if (!options.Enabled && string.IsNullOrWhiteSpace(options.PublicUrl))
            logger.LogWarning(
                "Client module {Key} registered, but the gamedata host is off (Turbo:Gamedata:Enabled), so clients can't load it",
                source.Key
            );
        else
            logger.LogInformation(
                "Client module {Key}@{Version} registered: {Entry}",
                source.Key,
                source.Version,
                source.Entry
            );

        Changed?.Invoke();

        return new Registration(this, source);
    }

    public string? FindFile(string key, string path) =>
        _modules.TryGetValue(key, out var source) ? Resolve(source.Root, path) : null;

    public string HashOf(string fullPath)
    {
        var info = new FileInfo(fullPath);
        var length = info.Length;
        var written = info.LastWriteTimeUtc;

        if (
            _hashes.TryGetValue(fullPath, out var known)
            && known.Length == length
            && known.Written == written
        )
            return known.Hash;

        using var stream = info.OpenRead();
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));

        _hashes[fullPath] = (length, written, hash);

        return hash;
    }

    private void Remove(ClientModuleSource source)
    {
        lock (_lock)
        {
            // Only this registration: a newer one under the same key is left alone.
            if (!_modules.TryGetValue(source.Key, out var current) || current != source)
                return;

            _modules = _modules.Remove(source.Key);
        }

        foreach (var path in _hashes.Keys.Where(x => IsUnder(source.Root, x)))
            _hashes.TryRemove(path, out _);

        logger.LogInformation("Client module {Key} taken away", source.Key);

        Changed?.Invoke();
    }

    /// <summary>The module as clients are told of it; null while its entry file is gone.</summary>
    private ClientModuleSnapshot? Snapshot(ClientModuleSource source)
    {
        var file = Resolve(source.Root, source.Entry);

        if (file is null)
            return null;

        var hash = HashOf(file);
        var publicUrl = config.CurrentValue.PublicUrl;
        var host = string.IsNullOrWhiteSpace(publicUrl) ? string.Empty : publicUrl.TrimEnd('/');
        var path = string.Join('/', source.Entry.Split('/').Select(Uri.EscapeDataString));

        return new ClientModuleSnapshot
        {
            Key = source.Key,
            Url =
                $"{host}{GamedataServer.PLUGIN_FILES_ROUTE}{Uri.EscapeDataString(source.Key)}/{path}?v={hash[..VERSION_LENGTH]}",
            Version = source.Version,
            Sha256 = hash,
        };
    }

    /// <summary>
    /// The full path of <paramref name="path"/> under <paramref name="root"/>; null when it leaves
    /// the folder, names a hidden file or folder (<c>.git</c>, <c>.env</c>), or is not a file.
    /// </summary>
    internal static string? Resolve(string root, string path)
    {
        var normalized = Normalize(path);

        if (normalized.Length == 0 || normalized.Split('/').Any(x => x.Length == 0 || x[0] == '.'))
            return null;

        var full = Path.GetFullPath(Path.Combine(root, normalized));

        return IsUnder(root, full) && File.Exists(full) ? full : null;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private static bool IsUnder(string root, string full) =>
        full.StartsWith(
            root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        );

    private sealed class Registration(ClientModuleRegistry owner, ClientModuleSource source)
        : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Remove(source);
        }
    }
}
