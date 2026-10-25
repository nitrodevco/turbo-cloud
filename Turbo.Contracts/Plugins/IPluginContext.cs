using System;
using System.Threading;

namespace Turbo.Contracts.Plugins;

/// <summary>What a <see cref="TurboPlugin"/> is handed as it starts and stops.</summary>
public interface IPluginContext
{
    PluginManifest Manifest { get; }

    /// <summary>The plugin's own services; the host's are reached through <see cref="Host{T}"/>.</summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Cancelled as the plugin starts to stop (a reload, a disable, the server stopping), before
    /// its hosted services and <c>StoppingAsync</c>: the token for work the plugin starts itself
    /// and that must not outlive it. A plugin needs no token source of its own.
    /// </summary>
    CancellationToken Stopping => CancellationToken.None;

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
