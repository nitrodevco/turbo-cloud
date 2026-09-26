using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A one-way gate: an avatar on the tile in front of it may pass through to the tile behind.
/// Entering opens the gate, walks the avatar through, and closes it again once it is clear.
/// The gate itself is never toggled by a plain use.
/// </summary>
[RoomObjectLogic("one_way_door")]
public class FurnitureOneWayDoorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    public override bool CanWalk() => GetState() == GateStates.OPEN;

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not EnterOneWayDoorInteraction)
            return false;

        if (GetState() == GateStates.OPEN)
            return false;

        var avatar = GetAvatar(ctx);

        if (avatar is null || avatar.IsWalking)
            return false;

        var map = _roomGrain.MapModule;
        var doorIdx = _ctx.GetTileIdx();
        var rotation = _ctx.RoomObject.Rotation;

        // The entry side faces the gate's rotation; the exit is the tile directly behind it.
        if (
            !map.TryGetTileInFront(doorIdx, rotation, out var frontIdx)
            || !map.TryGetTileInFront(doorIdx, rotation.Opposite(), out var backIdx)
            || map.ToIdx(avatar.X, avatar.Y) != frontIdx
        )
            return false;

        var exitTile = await _roomGrain.GetTileSnapshotAsync(backIdx, ct);

        if (
            !exitTile.Flags.Has(RoomTileFlags.Walkable)
            || exitTile.Flags.Has(RoomTileFlags.AvatarOccupied)
        )
            return false;

        await SetStateAsync(GateStates.OPEN);
        await SendStatusAsync(GateStates.OPEN);

        var (exitX, exitY) = map.GetTileXY(backIdx);

        await _roomGrain.AvatarModule.WalkAvatarToAsync(avatar, exitX, exitY, ct);

        _roomGrain.TimerSystem.Schedule(
            _ctx.ObjectId,
            _roomGrain._roomConfig.OneWayDoorCloseMs,
            CloseAsync
        );

        return true;
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);

        return base.OnPickupAsync(ctx, ct);
    }

    private async Task CloseAsync(CancellationToken ct)
    {
        var tile = await _ctx.GetTileSnapshotAsync(ct);

        // Never close on top of someone still crossing; try again shortly.
        if (tile.Flags.Has(RoomTileFlags.AvatarOccupied))
        {
            _roomGrain.TimerSystem.Schedule(
                _ctx.ObjectId,
                _roomGrain._roomConfig.OneWayDoorCloseMs,
                CloseAsync
            );

            return;
        }

        await SetStateAsync(GateStates.CLOSED);
        await SendStatusAsync(GateStates.CLOSED);
    }

    private Task SendStatusAsync(int status) =>
        _ctx.SendComposerToRoomAsync(
            new OneWayDoorStatusMessageComposer { FurniId = _ctx.ObjectId, Status = status }
        );
}
