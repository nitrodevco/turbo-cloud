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
using Turbo.Primitives.Orleans.States.Players;
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

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        Console.WriteLine($"PlayerPresenceGrain activated: {this.GetPrimaryKeyLong()}");
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        Console.WriteLine($"PlayerPresenceGrain deactivated: {this.GetPrimaryKeyLong()}");
    }

    public Task<SessionKey> GetSessionKeyAsync() =>
        Task.FromResult(SessionKey.From(_state.State.Session.Value));

    public Task<RoomPointerSnapshot> GetActiveRoomAsync() =>
        Task.FromResult(
            new RoomPointerSnapshot
            {
                RoomId = RoomId.From(_state.State.ActiveRoomId.Value),
                ActiveSinceUtc = _state.State.ActiveRoomSinceUtc,
            }
        );

    public Task<RoomPendingSnapshot> GetPendingRoomAsync() =>
        Task.FromResult(
            new RoomPendingSnapshot
            {
                RoomId = RoomId.From(_state.State.PendingRoomId.Value),
                Approved = _state.State.PendingRoomApproved,
            }
        );

    public async Task RegisterSessionAsync(SessionKey key, ISessionContextObserver observer)
    {
        _sessionObserver = observer;

        _state.State.Session = SessionKey.From(key.Value);

        _inventoryModule.OnSessionAttached();

        await _state.WriteStateAsync();
    }

    public async Task UnregisterSessionAsync(SessionKey key, CancellationToken ct)
    {
        if (!_state.State.Session.CompareTo(key))
            return;

        _inventoryModule.OnSessionAttached();

        _sessionObserver = null;

        // reset the persistent state?

        _state.State.Session = SessionKey.Empty;

        await _state.WriteStateAsync(ct);
        await ClearActiveRoomAsync(ct);
    }

    public async Task SetActiveRoomAsync(RoomId roomId, CancellationToken ct)
    {
        var prev = _state.State.ActiveRoomId;
        var next = roomId;
        var changed = !prev.CompareTo(next);

        _state.State.ActiveRoomId = RoomId.From(next.Value);
        _state.State.PendingRoomId = RoomId.Empty;
        _state.State.PendingRoomApproved = false;
        _state.State.ActiveRoomSinceUtc = DateTime.UtcNow;

        await _state.WriteStateAsync(ct);

        if (changed)
        {
            if (!prev.IsEmpty())
                await _roomService
                    .GetRoomDirectory()
                    .RemovePlayerFromRoomAsync(this.GetPrimaryKeyLong(), prev, ct);

            if (!next.IsEmpty())
                await _roomService
                    .GetRoomDirectory()
                    .AddPlayerToRoomAsync(this.GetPrimaryKeyLong(), next, ct);
        }
    }

    public Task ClearActiveRoomAsync(CancellationToken ct) => SetActiveRoomAsync(RoomId.Empty, ct);

    public async Task LeaveRoomAsync(RoomId roomId, CancellationToken ct)
    {
        if (!_state.State.ActiveRoomId.CompareTo(roomId))
            return;

        await SetActiveRoomAsync(RoomId.Empty, ct);
    }

    public async Task SetPendingRoomAsync(RoomId roomId, bool approved)
    {
        _state.State.PendingRoomId = RoomId.From(roomId.Value);
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
