using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Plugins;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// Gamedata files with the theme-specific views the audience and live-reload services need.
/// The file and theme views share their cache and invalidation lifecycle. A replacement provider
/// implements this capability; the host aliases <see cref="IGamedataFileService"/> to it.
/// </summary>
public interface IThemeFileService : IGamedataFileService
{
    /// <summary>Raised after <see cref="IGamedataFileService.Invalidate"/> marks a file changed.</summary>
    public event Action<string>? Invalidated;

    /// <summary>
    /// The variables for a theme's inheritance chain; null when the default file already has them.
    /// </summary>
    public Task<IReadOnlyDictionary<string, string>?> ThemeVariablesAsync(
        string? theme,
        CancellationToken ct
    );

    /// <summary>The merged look for a theme's chain, with its content revision.</summary>
    public Task<ThemeLook> ThemeLookAsync(string? theme, CancellationToken ct);

    /// <summary>A recently built look by revision; null when the provider no longer keeps it.</summary>
    public ThemeLook? FindLook(string revision);
}
