using System;

namespace Turbo.Contracts.Plugins;

/// <summary>What a <see cref="TurboPlugin"/> is handed as it starts and stops.</summary>
public interface IPluginContext
{
    PluginManifest Manifest { get; }

    /// <summary>The plugin's own services; the host's are reached through <see cref="Host{T}"/>.</summary>
    IServiceProvider Services { get; }

    /// <summary>One of the host's services, as <see cref="IHostServices"/> gives it.</summary>
    T Host<T>()
        where T : notnull;

    /// <summary>
    /// Disposes <paramref name="registration"/> when the plugin stops (last tracked first), and
    /// hands it back.
    /// </summary>
    T Track<T>(T registration)
        where T : IDisposable;
}
