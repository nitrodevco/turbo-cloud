using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Runtime;

public sealed class AsyncSignal
{
    private readonly TaskCompletionSource _tcs = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

#pragma warning disable VSTHRD003
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) =>
        Task.WhenAny(_tcs.Task, Task.Delay(timeout, ct));
#pragma warning restore VSTHRD003

    public void Set() => _tcs.TrySetResult();
}
