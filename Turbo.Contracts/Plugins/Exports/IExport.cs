using System;

namespace Turbo.Contracts.Plugins.Exports;

public interface IExport<T>
    where T : class
{
    T Current { get; }
    IDisposable Subscribe(Action<T> onSwap);
}
