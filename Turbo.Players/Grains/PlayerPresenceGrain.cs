using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Streams;
using Turbo.Players.Grains.Modules;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
    : Grain,
        IPlayerPresenceGrain,
        IAsyncObserver<RoomOutbound>
{
    internal readonly IGrainFactory _grainFactory;
    internal readonly PlayerPresenceLiveState _state;

    private readonly PlayerInventoryModule _inventoryModule;
    private readonly PlayerWalletModule _walletModule;

    private ISessionContextObserver? _sessionObserver = null;
    private StreamSubscriptionHandle<RoomOutbound>? _roomOutboundSub = null;

    private readonly Queue<IComposer> _outgoingQueue = new();
    private bool _isProcessingQueue = false;

    public PlayerPresenceGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;

        _state = new();
        _inventoryModule = new(this);
        _walletModule = new(this);
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _outgoingQueue.Clear();

        return Task.CompletedTask;
    }

    public async Task RegisterSessionObserverAsync(ISessionContextObserver observer)
    {
        _sessionObserver = observer;

        await _inventoryModule.OnSessionAttachedAsync(CancellationToken.None);
    }

    public async Task UnregisterSessionObserverAsync(CancellationToken ct)
    {
        await ClearActiveRoomAsync(ct);

        await _inventoryModule.OnSessionDetachedAsync(ct);

        _sessionObserver = null;
    }

    public Task SendComposerAsync(IComposer composer)
    {
        if (composer is not null)
        {
            _outgoingQueue.Enqueue(composer);

            _ = ProcessOutgoingQueueAsync();
        }

        return Task.CompletedTask;
    }

    public Task SendComposerAsync(params IComposer[] composers)
    {
        if (composers.Length > 0)
        {
            foreach (var composer in composers)
                _outgoingQueue.Enqueue(composer);

            _ = ProcessOutgoingQueueAsync();
        }

        return Task.CompletedTask;
    }

    public Task OnNextAsync(RoomOutbound item, StreamSequenceToken? token = null)
    {
        if (
            _sessionObserver is null
            || item.ExcludedPlayerIds is not null
                && item.ExcludedPlayerIds.Contains((int)this.GetPrimaryKeyLong())
        )
            return Task.CompletedTask;

        return SendComposerAsync(item.Composer);
    }

    public Task OnCompletedAsync() => Task.CompletedTask;

    public Task OnErrorAsync(Exception ex) => Task.CompletedTask;

    private async Task ProcessOutgoingQueueAsync()
    {
        if (_isProcessingQueue)
            return;

        _isProcessingQueue = true;

        await Task.Yield();

        if (_sessionObserver is not null)
        {
            while (_outgoingQueue.Count > 0)
            {
                var payload = _outgoingQueue.Dequeue();

                await _sessionObserver.SendComposerAsync(payload);
            }
        }

        _isProcessingQueue = false;
    }
}
