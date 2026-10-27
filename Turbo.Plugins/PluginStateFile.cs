using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Turbo.Plugins;

/// <summary>
/// The operator's choices about plugins that outlive a restart, kept beside the plugins in
/// <c>plugins.json</c> (<c>{"Disabled": ["key"], "Sources": [...]}</c>), so they travel with the
/// folder and can be edited by hand. Discovery reads folders only, so the file is never taken for a
/// plugin. A source's token is sealed (<see cref="PluginSecrets"/>), never written as typed.
/// </summary>
internal sealed class PluginStateFile(string pluginFolderPath)
{
    public const string FILE_NAME = "plugins.json";

    private static readonly JsonSerializerOptions JSON = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private readonly Lock _lock = new();

    public string Path { get; } = System.IO.Path.Combine(pluginFolderPath, FILE_NAME);

    /// <summary>The keys turned off, read from disk each time so a hand edit counts at the next scan.</summary>
    public ImmutableHashSet<string> Disabled
    {
        get
        {
            lock (_lock)
                return ImmutableHashSet.CreateRange(
                    StringComparer.OrdinalIgnoreCase,
                    Read().Disabled
                );
        }
    }

    /// <summary>The GitHub repositories plugins are linked to, as last saved.</summary>
    public ImmutableArray<PluginSourceEntry> Sources
    {
        get
        {
            lock (_lock)
                return [.. Read().Sources];
        }
    }

    public void SetDisabled(string key, bool disabled)
    {
        lock (_lock)
        {
            var state = Read();
            var keys = new SortedSet<string>(state.Disabled, StringComparer.OrdinalIgnoreCase);

            if (!(disabled ? keys.Add(key) : keys.Remove(key)))
                return;

            Write(new State { Disabled = [.. keys], Sources = state.Sources });
        }
    }

    /// <summary>Changes the sources and saves them, read and written under one lock.</summary>
    public T EditSources<T>(Func<List<PluginSourceEntry>, T> edit)
    {
        lock (_lock)
        {
            var state = Read();
            var sources = state.Sources.ToList();
            var result = edit(sources);

            Write(new State { Disabled = state.Disabled, Sources = [.. sources] });

            return result;
        }
    }

    /// <summary>
    /// Notes that the plugin <paramref name="key"/> was rolled back to <paramref name="version"/>:
    /// a link it came from shows its release as an update again, and stops taking that release on
    /// its own (a newer one is taken).
    /// </summary>
    public void RolledBack(string key, string version) =>
        EditSources(sources =>
        {
            for (var i = 0; i < sources.Count; i++)
                if (string.Equals(sources[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    sources[i] = sources[i] with
                    {
                        SkipTag = sources[i].InstalledTag ?? sources[i].SkipTag,
                        InstalledTag = null,
                        InstalledVersion = version,
                    };

            return 0;
        });

    private void Write(State state)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);

        var temp = Path + ".tmp";

        File.WriteAllText(temp, JsonSerializer.Serialize(state, JSON));
        File.Move(temp, Path, overwrite: true);
    }

    private State Read()
    {
        if (!File.Exists(Path))
            return new State();

        var state = JsonSerializer.Deserialize<State>(File.ReadAllText(Path), JSON) ?? new State();

        return new State
        {
            Disabled = [.. state.Disabled.Where(k => !string.IsNullOrWhiteSpace(k))],
            Sources =
            [
                .. state.Sources.Where(x =>
                    !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Repository)
                ),
            ],
        };
    }

    private sealed class State
    {
        public string[] Disabled { get; init; } = [];

        public PluginSourceEntry[] Sources { get; init; } = [];
    }
}

/// <summary>
/// A GitHub repository a plugin is installed from, as kept in <c>plugins.json</c>: what it
/// follows, what is installed from it and what GitHub last offered.
/// </summary>
internal sealed record PluginSourceEntry
{
    public string Id { get; init; } = "";

    /// <summary><c>owner/repo</c>.</summary>
    public string Repository { get; init; } = "";

    /// <summary><c>latest</c> (the newest release) or <c>tag</c> (<see cref="Tag"/>'s release only).</summary>
    public string Follow { get; init; } = PluginSourceFollow.LATEST;

    public string? Tag { get; init; }

    /// <summary>
    /// The folder in the repository holding the plugin's manifest.json (<c>""</c> its root);
    /// null takes the one folder with a plugin manifest.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>The token, sealed and base64; null for a public repository.</summary>
    public string? Token { get; init; }

    public bool AutoUpdate { get; init; }

    /// <summary>The plugin's key, once a release has been installed.</summary>
    public string? Key { get; init; }

    public string? InstalledTag { get; init; }

    public string? InstalledVersion { get; init; }

    public string? LatestTag { get; init; }

    public string? LatestUrl { get; init; }

    public DateTime? LatestPublishedAtUtc { get; init; }

    public DateTime? CheckedAtUtc { get; init; }

    /// <summary>Why the last check or update failed; null when it worked.</summary>
    public string? Error { get; init; }

    /// <summary>A release rolled back from: automatic updates leave it alone; a newer one is taken.</summary>
    public string? SkipTag { get; init; }
}

internal static class PluginSourceFollow
{
    public const string LATEST = "latest";
    public const string TAG = "tag";
}
