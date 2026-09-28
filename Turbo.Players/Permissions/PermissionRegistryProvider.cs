using System;
using System.Collections.Generic;
using System.Threading;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Players.Permissions;

/// <summary>
/// Holds the live registry. Core's source is always in it; plugin sources come and go through
/// <see cref="Register"/>, each registration building a whole new registry so a reader never sees
/// one half changed. Player permission grains compare <see cref="Current"/> by reference and
/// resolve again when it moves.
/// </summary>
internal sealed class PermissionRegistryProvider : IPermissionRegistryProvider
{
    private readonly Lock _lock = new();
    private readonly List<IPermissionNodeSource> _sources = [new CorePermissionNodeSource()];

    private PermissionRegistry _current;

    public PermissionRegistryProvider()
    {
        _current = new PermissionRegistry(_sources);
    }

    public PermissionRegistry Current => Volatile.Read(ref _current);

    public IDisposable Register(IPermissionNodeSource source)
    {
        lock (_lock)
        {
            // Built before it is kept, so a clashing source throws with nothing changed.
            var next = new PermissionRegistry([.. _sources, source]);

            _sources.Add(source);
            Volatile.Write(ref _current, next);
        }

        return new Registration(this, source);
    }

    private void Unregister(IPermissionNodeSource source)
    {
        lock (_lock)
        {
            if (!_sources.Remove(source))
                return;

            Volatile.Write(ref _current, new PermissionRegistry(_sources));
        }
    }

    private sealed class Registration(
        PermissionRegistryProvider provider,
        IPermissionNodeSource source
    ) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                provider.Unregister(source);
        }
    }
}
