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
/// <c>plugins.json</c> (<c>{"Disabled": ["key"]}</c>), so they travel with the folder and can be
/// edited by hand. Discovery reads folders only, so the file is never taken for a plugin.
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

    public void SetDisabled(string key, bool disabled)
    {
        lock (_lock)
        {
            var state = Read();
            var keys = new SortedSet<string>(state.Disabled, StringComparer.OrdinalIgnoreCase);

            if (!(disabled ? keys.Add(key) : keys.Remove(key)))
                return;

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);

            var temp = Path + ".tmp";

            File.WriteAllText(
                temp,
                JsonSerializer.Serialize(new State { Disabled = [.. keys] }, JSON)
            );
            File.Move(temp, Path, overwrite: true);
        }
    }

    private State Read()
    {
        if (!File.Exists(Path))
            return new State();

        var state = JsonSerializer.Deserialize<State>(File.ReadAllText(Path), JSON) ?? new State();

        return new State
        {
            Disabled = [.. state.Disabled.Where(k => !string.IsNullOrWhiteSpace(k))],
        };
    }

    private sealed class State
    {
        public string[] Disabled { get; init; } = [];
    }
}
