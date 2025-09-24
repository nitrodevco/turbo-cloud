using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Exports;

namespace Turbo.Plugins.Exports;

public sealed class ReloadableExport<T> : IExport<T>
    where T : class
{
    private volatile T? _current;
    private readonly Lock _gate = new();
    private readonly List<Action<T>> _subs = new();

    public T Current =>
        _current ?? throw new InvalidOperationException($"Export {typeof(T).Name} not bound yet.");

    public async Task SwapAsync(T value)
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
                await a.DisposeAsync().ConfigureAwait(false);
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
        T? current;

        lock (_gate)
        {
            _subs.Add(onSwap);
            current = _current;
        }

        // Invoke outside the lock to avoid potential deadlocks in subscriber callbacks.
        if (current is not null)
            try
            {
                onSwap(current);
            }
            catch { }

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
