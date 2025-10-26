using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Runtime;

public sealed class AsyncSignal
{
    private readonly TaskCompletionSource _tcs = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) =>
        Task.WhenAny(_tcs.Task, Task.Delay(timeout, ct));

    public void Set() => _tcs.TrySetResult();
}
