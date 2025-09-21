using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Contracts.Plugins.Exports;

namespace Turbo.Plugins.Exports;

public sealed class ReloadableExport<T> : IExport<T>
    where T : class
{
    private volatile T? _current;
    private readonly object _gate = new();
    private readonly List<Action<T>> _subs = new();

    public T Current =>
        _current ?? throw new InvalidOperationException($"Export {typeof(T).Name} not bound yet.");

    public void Swap(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        List<Action<T>> subs;
        lock (_gate)
        {
            _current = value;
            subs = [.. _subs];
        }
        foreach (var s in subs)
            s(value);
    }

    public IDisposable Subscribe(Action<T> onSwap)
    {
        lock (_gate)
        {
            _subs.Add(onSwap);
            if (_current is not null)
                onSwap(_current);
        }
        return new Unsub(() =>
        {
            lock (_gate)
                _subs.Remove(onSwap);
        });
    }

    private sealed class Unsub(Action a) : IDisposable
    {
        private readonly Action _a = a;

        public void Dispose() => _a();
    }
}
