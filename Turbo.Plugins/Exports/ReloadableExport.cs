using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Exports;

namespace Turbo.Plugins.Exports;

public sealed class ReloadableExport<T> : IExport<T>
    where T : class
{
    private volatile T? _current;
    private ImmutableArray<Action<T>> _subs = [];

    public T Current =>
        _current ?? throw new InvalidOperationException($"Export {typeof(T).Name} not bound yet.");

    public async Task SwapAsync(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var previous = Interlocked.Exchange(ref _current, value);
        var subs = _subs;

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
        ImmutableInterlocked.Update(ref _subs, a => a.Add(onSwap));

        var current = _current;
        if (current is not null)
        {
            try
            {
                onSwap(current);
            }
            catch { }
        }

        return new Unsub(() => ImmutableInterlocked.Update(ref _subs, a => a.Remove(onSwap)));
    }

    private sealed class Unsub(Action a) : IDisposable
    {
        private readonly Action _a = a;

        public void Dispose() => _a();
    }
}
