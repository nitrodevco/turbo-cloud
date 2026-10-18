using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A one-way gate, as Habbo has it (official client capture 2026-10-17, a gate at direction 2):
/// only an avatar on the tile its direction points at may enter. Entering sends OneWayDoorStatus
/// 1, walks the avatar through to the tile behind, and sends 0 as it arrives there; from behind
/// nothing happens. Open or shut is only that message: the item's own data stays as it is and
/// no tile height changes, so nothing else is sent. The gate is never toggled by a plain use.
/// The other directions are derived: the entry is always the tile the direction points at.
/// </summary>
[RoomObjectLogic("one_way_door")]
public class FurnitureOneWayDoorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    private RoomObjectId? _passing;

    public override bool CanWalk() => _passing is not null;

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

        if (_passing is not null)
            return false;

        var avatar = GetAvatar(ctx);

        // Standing on the front tile, or on the last step of a walk there: the walk through then
        // starts from it, as the client sends this right after the walk that brought it there.
        if (avatar is null || (avatar.IsWalking && avatar.TilePath.Count > 0))
            return false;

        var map = MapModule;
        var doorIdx = _ctx.GetTileIdx();
        var rotation = _ctx.RoomObject.Rotation;

        // The entry side faces the gate's rotation; the exit is the tile directly behind it.
        if (
            !map.TryGetTileInFront(doorIdx, rotation, out var frontIdx)
            || !map.TryGetTileInFront(doorIdx, rotation.Opposite(), out var backIdx)
            || (avatar.NextTileId >= 0 ? avatar.NextTileId : map.ToIdx(avatar.X, avatar.Y))
                != frontIdx
        )
            return false;

        // Floor or furni stood on, and nobody there. (Walkable alone is set only on furni tiles,
        // so asking for it refused every gate in front of plain floor.)
        if (!map.CanAvatarWalk(avatar, backIdx))
            return false;

        _passing = avatar.ObjectId;
        _ctx.RefreshTile();

        await SendStatusAsync(GateStates.OPEN);

        var (exitX, exitY) = map.GetTileXY(backIdx);

        // Shut again even if the avatar never steps in (its walk was taken over).
        TimerSystem.Schedule(_ctx.ObjectId, _roomGrain._roomConfig.OneWayDoorCloseMs, CloseAsync);

        if (!await AvatarModule.WalkAvatarToAsync(avatar, exitX, exitY, ct))
        {
            await CloseAsync(ct);

            return false;
        }

        return true;
    }

    public override async Task OnWalkOffAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOffAsync(ctx, ct);

        // Off the gate onto the tile behind: shut as the step there ends, when it arrives.
        if (_passing == ctx.ObjectId)
            TimerSystem.Schedule(_ctx.ObjectId, _roomGrain._roomConfig.AvatarTickMs, CloseAsync);
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _passing = null;

        return base.OnPickupAsync(ctx, ct);
    }

    private async Task CloseAsync(CancellationToken ct)
    {
        var tile = await _ctx.GetTileSnapshotAsync(ct);

        // Never close on top of someone still crossing; try again shortly.
        if (tile.Flags.Has(RoomTileFlags.AvatarOccupied))
        {
            TimerSystem.Schedule(
                _ctx.ObjectId,
                _roomGrain._roomConfig.OneWayDoorCloseMs,
                CloseAsync
            );

            return;
        }

        if (_passing is null)
            return;

        _passing = null;
        _ctx.RefreshTile();

        await SendStatusAsync(GateStates.CLOSED);
    }

    private Task SendStatusAsync(int status) =>
        _ctx.SendComposerToRoomAsync(
            new OneWayDoorStatusMessageComposer { FurniId = _ctx.ObjectId, Status = status }
        );
}
