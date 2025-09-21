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
        object? previous = null;
        lock (_gate)
        {
            previous = _current;
            _current = value;
            subs = [.. _subs];
        }

        try
        {
            if (previous is IAsyncDisposable a)
                a.DisposeAsync().AsTask().GetAwaiter().GetResult();
            else if (previous is IDisposable d)
                d.Dispose();
        }
        catch { }

        foreach (var s in subs)
        {
            try
            {
                s(value);
            }
            catch { }
        }
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
