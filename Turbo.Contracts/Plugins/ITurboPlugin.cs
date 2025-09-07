using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins;

public interface ITurboPlugin : IAsyncDisposable
{
    ValueTask OnEnableAsync(CancellationToken ct);
    ValueTask OnDisableAsync(CancellationToken ct);
}
