using System;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Orleans;
using Turbo.Primitives.Grains;
using Turbo.Primitives.Snapshots.Rooms;
using Turbo.Primitives.States.Rooms;

namespace Turbo.Rooms;

public class PlayerPresenceGrain(
    [PersistentState(OrleansStateNames.PLAYER_PRESENCE, OrleansStorageNames.PRESENCE_STORE)]
        IPersistentState<PlayerPresenceState> state,
    IGrainFactory grainFactory
) : Grain, IPlayerPresenceGrain
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public Task<RoomPointerSnapshot> GetCurrentRoomAsync() =>
        Task.FromResult(
            new RoomPointerSnapshot { RoomId = state.State.RoomId, Since = state.State.Since }
        );

    public Task<PendingRoomInfoSnapshot> GetPendingRoomAsync() =>
        Task.FromResult(
            new PendingRoomInfoSnapshot
            {
                RoomId = state.State.PendingRoomId,
                Approved = state.State.PendingRoomApproved,
            }
        );

    public async Task ResetAsync() => await state.ClearStateAsync();

    public async Task SetPendingRoomAsync(long roomId, bool approved)
    {
        state.State.PendingRoomId = roomId;
        state.State.PendingRoomApproved = approved;

        await state.WriteStateAsync();
    }

    public async Task<RoomChangedSnapshot> EnterRoomAsync(long roomId)
    {
        var prev = state.State.RoomId;

        if (prev == roomId)
            return new RoomChangedSnapshot
            {
                PreviousRoomId = prev,
                CurrentRoomId = prev,
                Changed = false,
            };

        if (prev > 0)
            await _grainFactory
                .GetGrain<IRoomPresenceGrain>(prev)
                .RemovePlayerIdAsync(this.GetPrimaryKeyLong());

        await _grainFactory
            .GetGrain<IRoomPresenceGrain>(roomId)
            .AddPlayerIdAsync(this.GetPrimaryKeyLong());

        state.State.RoomId = roomId;
        state.State.Since = DateTimeOffset.UtcNow;
        state.State.PendingRoomId = -1;
        state.State.PendingRoomApproved = false;

        await state.WriteStateAsync();

        return new RoomChangedSnapshot
        {
            PreviousRoomId = prev,
            CurrentRoomId = roomId,
            Changed = true,
        };
    }

    public async Task<RoomChangedSnapshot> LeaveRoomAsync()
    {
        var prev = state.State.RoomId;

        if (prev <= 0)
            return new RoomChangedSnapshot
            {
                PreviousRoomId = -1,
                CurrentRoomId = -1,
                Changed = false,
            };

        await _grainFactory
            .GetGrain<IRoomPresenceGrain>(prev)
            .RemovePlayerIdAsync(this.GetPrimaryKeyLong());

        state.State.RoomId = -1;
        state.State.Since = DateTimeOffset.UtcNow;

        await state.WriteStateAsync();

        return new RoomChangedSnapshot
        {
            PreviousRoomId = prev,
            CurrentRoomId = -1,
            Changed = true,
        };
    }
}
