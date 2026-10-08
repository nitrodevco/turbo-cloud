using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A tile that sends whoever steps onto it to another one in the room, chosen at random (Sulake's
/// <c>RandomInRoomTeleportFurniture</c>: the Banzai teleporter, "Where you go nobody knows", and
/// the Ghost Hotel and Halloween 2013 tiles). Any other furni of this logic whose tile the avatar
/// could stand on is a destination; with none, nothing happens. Both light up (state 1) for
/// <c>RandomTeleportFlashMs</c>. The avatar is sent once its step onto the tile is over, so one
/// running across is sent too; landing on the other one does not send it on again.
/// </summary>
[RoomObjectLogic("random_teleport")]
public class FurnitureRandomTeleportLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int IDLE_STATE = 0;
    private const int FLASH_STATE = 1;

    /// <summary>Avatars that stepped on it, sent on the next timer run.</summary>
    private readonly HashSet<RoomObjectId> _arriving = [];

    /// <summary>Avatars sent here, whose landing is not a step onto it.</summary>
    private readonly HashSet<RoomObjectId> _landing = [];

    /// <summary>When it stops being lit; 0 when it is not.</summary>
    private long _flashUntilMs;

    public override async Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOnAsync(ctx, ct);

        if (ctx.RoomObject is not IRoomAvatar avatar || _landing.Remove(avatar.ObjectId))
            return;

        _arriving.Add(avatar.ObjectId);
        TimerSystem.Schedule(_ctx.ObjectId, 0, TickAsync);
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _arriving.Clear();
        _landing.Clear();
        _flashUntilMs = 0;

        return base.OnPickupAsync(ctx, ct);
    }

    /// <summary>
    /// Sends whoever stepped on it and puts it back to idle once its light is out; runs while
    /// either is pending.
    /// </summary>
    private async Task TickAsync(CancellationToken ct)
    {
        await SendArrivingAsync(ct);

        if (_flashUntilMs > 0 && _roomGrain.NowMs() >= _flashUntilMs)
        {
            _flashUntilMs = 0;

            if (GetState() != IDLE_STATE)
                await SetStateAsync(IDLE_STATE);
        }

        if (_arriving.Count > 0 || _flashUntilMs > 0)
            TimerSystem.Schedule(_ctx.ObjectId, _roomGrain._roomConfig.AvatarTickMs, TickAsync);
    }

    private async Task SendArrivingAsync(CancellationToken ct)
    {
        var tileIdx = _ctx.GetTileIdx();

        foreach (var objectId in _arriving.ToArray())
        {
            _arriving.Remove(objectId);

            if (
                !AvatarModule.TryGetAvatar(objectId, out var avatar)
                || TileOf(avatar) != tileIdx
                || PickDestination(avatar) is not { } destination
            )
                continue;

            destination._landing.Add(objectId);

            await AvatarModule.RelocateAvatarAsync(avatar, destination._ctx.GetTileIdx(), ct);

            avatar.MarkDirty();

            await FlashAsync();
            await destination.FlashAsync();
        }
    }

    private FurnitureRandomTeleportLogic? PickDestination(IRoomAvatar avatar)
    {
        var destinations = _roomGrain
            ._state.ItemsById.Values.OfType<IRoomFloorItem>()
            .Select(item => item.Logic)
            .OfType<FurnitureRandomTeleportLogic>()
            .Where(other =>
                other != this && MapModule.CanAvatarWalk(avatar, other._ctx.GetTileIdx())
            )
            .ToArray();

        return destinations.Length == 0
            ? null
            : destinations[Random.Shared.Next(destinations.Length)];
    }

    private async Task FlashAsync()
    {
        if (_ctx.Definition.TotalStates <= FLASH_STATE)
            return;

        if (GetState() != FLASH_STATE)
            await SetStateAsync(FLASH_STATE);

        _flashUntilMs = _roomGrain.NowMs() + _roomGrain._roomConfig.RandomTeleportFlashMs;
        TimerSystem.Schedule(_ctx.ObjectId, _roomGrain._roomConfig.AvatarTickMs, TickAsync);
    }

    // Mid-step an avatar is already on the tile it steps onto, though its position is not.
    private int TileOf(IRoomAvatar avatar) =>
        avatar.NextTileId >= 0 ? avatar.NextTileId : MapModule.ToIdx(avatar.X, avatar.Y);
}
