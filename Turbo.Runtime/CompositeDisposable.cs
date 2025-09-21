using System;
using System.Collections.Generic;
using System.Threading;

namespace Turbo.Runtime;

public sealed class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> _items = [];
    private int _disposed;

    public void Add(IDisposable d)
    {
        if (d != null)
            _items.Add(d);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        for (var i = _items.Count - 1; i >= 0; i--)
            try
            {
                _items[i].Dispose();
            }
            catch
            { /* swallow on unload */
            }
    }
}
