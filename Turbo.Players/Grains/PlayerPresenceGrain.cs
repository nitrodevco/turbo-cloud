using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Orleans;
using Turbo.Players.Grains.Modules;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;

namespace Turbo.Players.Grains;

public sealed partial class PlayerPresenceGrain : Grain, IPlayerPresenceGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly IRoomService _roomService;
    private ISessionContextObserver? _sessionObserver = null;

    private IPersistentState<PlayerPresenceState> _state;
    private readonly PlayerInventoryModule _inventoryModule;

    public PlayerPresenceGrain(
        [PersistentState(OrleansStateNames.PLAYER_PRESENCE, OrleansStorageNames.PLAYER_STORE)]
            IPersistentState<PlayerPresenceState> state,
        IGrainFactory grainFactory,
        IRoomService roomService
    )
    {
        _grainFactory = grainFactory;
        _roomService = roomService;

        _state = state;
        _inventoryModule = new PlayerInventoryModule(this, grainFactory);
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<SessionKey> GetSessionKeyAsync() => Task.FromResult(_state.State.SessionKey);

    public Task<RoomPointerSnapshot> GetActiveRoomAsync() =>
        Task.FromResult(
            new RoomPointerSnapshot
            {
                RoomId = _state.State.ActiveRoomId,
                ActiveSinceUtc = _state.State.ActiveRoomSinceUtc,
            }
        );

    public Task<RoomPendingSnapshot> GetPendingRoomAsync() =>
        Task.FromResult(
            new RoomPendingSnapshot
            {
                RoomId = _state.State.PendingRoomId,
                Approved = _state.State.PendingRoomApproved,
            }
        );

    public async Task RegisterSessionAsync(SessionKey key, ISessionContextObserver observer)
    {
        _sessionObserver = observer;

        _state.State.SessionKey = key;

        await _inventoryModule.OnSessionAttachedAsync(CancellationToken.None);

        await _state.WriteStateAsync();
    }

    public async Task UnregisterSessionAsync(SessionKey key, CancellationToken ct)
    {
        if (!_state.State.SessionKey.Equals(key))
            return;

        await _inventoryModule.OnSessionDetachedAsync(ct);

        _sessionObserver = null;

        // reset the persistent state?

        _state.State.SessionKey = string.Empty;

        await _state.WriteStateAsync(ct);
        await ClearActiveRoomAsync(ct);
    }

    public async Task SetActiveRoomAsync(int roomId, CancellationToken ct)
    {
        var prev = _state.State.ActiveRoomId;
        var next = roomId;
        var changed = prev != next;

        _state.State.ActiveRoomId = next;
        _state.State.PendingRoomId = -1;
        _state.State.PendingRoomApproved = false;
        _state.State.ActiveRoomSinceUtc = DateTime.UtcNow;

        await _state.WriteStateAsync(ct);

        if (changed)
        {
            if (prev != -1)
                await _roomService
                    .GetRoomDirectory()
                    .RemovePlayerFromRoomAsync(this.GetPrimaryKeyLong(), prev, ct);

            if (next != -1)
                await _roomService
                    .GetRoomDirectory()
                    .AddPlayerToRoomAsync(this.GetPrimaryKeyLong(), next, ct);
        }
    }

    public Task ClearActiveRoomAsync(CancellationToken ct) => SetActiveRoomAsync(-1, ct);

    public async Task LeaveRoomAsync(int roomId, CancellationToken ct)
    {
        if (_state.State.ActiveRoomId != roomId)
            return;

        await SetActiveRoomAsync(-1, ct);
    }

    public async Task SetPendingRoomAsync(int roomId, bool approved)
    {
        _state.State.PendingRoomId = roomId;
        _state.State.PendingRoomApproved = approved;

        await _state.WriteStateAsync();
    }

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct)
    {
        if (_sessionObserver is null)
            return;

        await _sessionObserver.SendComposerAsync(composer, ct);
    }
}
