using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Turbo.Main.Settings;

/// <summary>
/// The admin panel's overrides as configuration, read whole from <see cref="_load"/>. It sits
/// above <c>appsettings.json</c> and below the environment and the command line
/// (<see cref="ServerSettingsConfiguration"/>). A list or map is overridden whole: the layers
/// below hide theirs (<see cref="ShadowedConfigurationProvider"/>), told which through
/// <paramref name="overrides"/>.
/// </summary>
internal sealed class ServerSettingsConfigurationProvider(
    Func<ServerSettingsLoad> load,
    ServerSettingsOverrides overrides
) : ConfigurationProvider
{
    private readonly Func<ServerSettingsLoad> _load = load;

    private HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Why the overrides couldn't be read when they were last read; null when they were.</summary>
    public string? LoadError { get; private set; }

    public override void Load()
    {
        var read = _load();
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var whole = new List<string>();

        if (read.Error is { } error)
            errors.Add(error);

        foreach (var (path, value) in read.Rows)
        {
            try
            {
                using var document = JsonDocument.Parse(value);

                if (document.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                    whole.Add(path);

                SettingJson.Flatten(path, document.RootElement, data);
                paths.Add(path);
            }
            catch (JsonException ex)
            {
                errors.Add($"{path}: {ex.Message}");
            }
        }

        LoadError = errors.Count == 0 ? null : string.Join("; ", errors);
        Data = data;
        _paths = paths;
        overrides.Replace(whole);
    }

    /// <summary>Whether the panel overrides the setting at the path, an empty list or map included.</summary>
    public bool Overrides(string path) => _paths.Contains(path);

    /// <summary>Reads the overrides again and tells the configuration they changed.</summary>
    public void Refresh()
    {
        Load();
        OnReload();
    }
}
