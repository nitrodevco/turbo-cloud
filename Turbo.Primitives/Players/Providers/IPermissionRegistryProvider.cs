using System;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Primitives.Players.Providers;

/// <summary>
/// The live <see cref="PermissionRegistry"/>: core's nodes, plus those of every plugin that is
/// loaded. A plugin load or unload replaces <see cref="Current"/> with a new registry; holders
/// compare the reference to know when to resolve again.
/// </summary>
public interface IPermissionRegistryProvider
{
    PermissionRegistry Current { get; }

    /// <summary>
    /// Raised after <see cref="Current"/> is replaced, on whichever thread loaded or unloaded the
    /// plugin. The permission group directory listens while it is active, so the player grains it
    /// knows of resolve again at once rather than on their next read.
    /// </summary>
    event System.Action? Changed;

    /// <summary>
    /// Adds a source. Disposing the result takes it out again.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The source clashes with one already registered or is malformed; nothing is added.
    /// </exception>
    IDisposable Register(IPermissionNodeSource source);
}
