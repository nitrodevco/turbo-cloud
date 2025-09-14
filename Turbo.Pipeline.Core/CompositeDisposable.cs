using System;
using System.Collections.Generic;
using System.Threading;

namespace Turbo.Pipeline.Core;

public class CompositeDisposable(IReadOnlyList<IDisposable> items) : IDisposable
{
    private IReadOnlyList<IDisposable>? _items = items;

    public void Dispose()
    {
        var items = Interlocked.Exchange(ref _items, null);
        if (items is null)
            return;
        foreach (var d in items)
            d.Dispose();
    }
}
