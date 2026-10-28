using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;

namespace Turbo.Gamedata.Assets;

/// <summary>Owns completed discovery snapshots, refresh coordination and background-read lifetime.</summary>
internal sealed class AssetDiscoveryCatalog(
    AssetDiscoveryService discovery,
    IOptions<AssetBundleConfig> config,
    TimeProvider time,
    IHostApplicationLifetime lifetime,
    ILogger<AssetDiscoveryCatalog> logger
) : IHostedService, IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _listing = new(1, 1);
    private readonly Lock _state = new();
    private readonly CancellationTokenSource _stopping =
        CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
    private AssetDiscoveryResult? _lists;
    private Task _background = Task.CompletedTask;
    private TaskCompletionSource? _drained;
    private Task? _stop;
    private Task? _dispose;
    private int _active;
    private bool _stopped;

    internal async Task<AssetDiscoveryResult> PreviewAsync(bool relist, CancellationToken ct)
    {
        Enter(ct);
        try
        {
            if (!relist && Volatile.Read(ref _lists) is { } cached)
            {
                if (
                    time.GetUtcNow().UtcDateTime - cached.ListedAt
                    >= TimeSpan.FromMinutes(Math.Max(0, config.Value.SyncPreviewMinutes))
                )
                    RefreshBehind();
                return cached;
            }
            return await RefreshAsync(_ => { }, probe: false, reuse: !relist, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            Leave();
        }
    }

    /// <summary>A sync always performs its probing pass, even after waiting for a preview.</summary>
    internal async Task<AssetDiscoveryResult> ForSyncAsync(Action<string> log, CancellationToken ct)
    {
        Enter(ct);
        try
        {
            return await RefreshAsync(log, probe: true, reuse: false, ct).ConfigureAwait(false);
        }
        finally
        {
            Leave();
        }
    }

    private async Task<AssetDiscoveryResult> RefreshAsync(
        Action<string> log,
        bool probe,
        bool reuse,
        CancellationToken ct
    )
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopping.Token);
        await _listing.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (reuse && Volatile.Read(ref _lists) is { } cached)
                return cached;
            return await ReadAsync(log, probe, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _listing.Release();
        }
    }

    private async Task<AssetDiscoveryResult> ReadAsync(
        Action<string> log,
        bool probe,
        CancellationToken ct
    )
    {
        var lists = await discovery.DiscoverAsync(log, probe, ct).ConfigureAwait(false);
        lock (_state)
        {
            ct.ThrowIfCancellationRequested();
            if (_stopped)
                throw new OperationCanceledException(_stopping.Token);
            Volatile.Write(ref _lists, lists);
        }
        return lists;
    }

    private void RefreshBehind()
    {
        lock (_state)
        {
            if (_stopped || _stopping.IsCancellationRequested || !_listing.Wait(0))
                return;
            Track();
            _background = Task.Run(async () =>
            {
                try
                {
                    await ReadAsync(_ => { }, probe: false, _stopping.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Reading Habbo's lists again for the sync preview failed: {Message}",
                        ex.Message
                    );
                }
                finally
                {
                    _listing.Release();
                    Leave();
                }
            });
        }
    }

    private void Enter(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_state)
        {
            if (_stopped || _stopping.IsCancellationRequested)
                throw new OperationCanceledException(_stopping.Token);
            Track();
        }
    }

    // The caller holds _state. Count queued requests too, so disposal cannot race a cancelled waiter.
    private void Track()
    {
        if (_active++ == 0)
            _drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void Leave()
    {
        lock (_state)
            if (--_active == 0)
                _drained!.TrySetResult();
    }

    internal Task WhenRefreshedAsync(CancellationToken ct)
    {
        lock (_state)
            return _background.WaitAsync(ct);
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct)
    {
        lock (_state)
        {
            _stopped = true;
            return (
                _stop ??= StopCoreAsync(_background, _drained?.Task ?? Task.CompletedTask)
            ).WaitAsync(ct);
        }
    }

    private async Task StopCoreAsync(Task background, Task drained)
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
#pragma warning disable VSTHRD003 // Both tasks belong to this catalog: background work runs on the pool and drain continuations run asynchronously.
        await Task.WhenAll(background, drained).ConfigureAwait(false);
#pragma warning restore VSTHRD003
    }

    public ValueTask DisposeAsync()
    {
        lock (_state)
            return new ValueTask(_dispose ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _listing.Dispose();
        _stopping.Dispose();
    }

#pragma warning disable VSTHRD002 // Synchronous DI disposal joins the same context-free shutdown; async hosts use StopAsync/DisposeAsync.
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
}
