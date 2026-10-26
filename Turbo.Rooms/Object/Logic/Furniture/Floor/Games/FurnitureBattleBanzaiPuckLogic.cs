using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Games;

/// <summary>
/// The Battle Banzai puck, <c>bb_puck</c> (Sulake's <c>BattleBallPuckFurniture</c>). As Habbo plays
/// it (official client capture 2026-10-26, session 012541 t 1465.6-1466.8, 1500.1-1500.6,
/// 1546.6-1547.6; 3/3 kicks): a player walking onto it kicks it on in the way they walk, one
/// tile at a time (an ObjectUpdate each, ~100-170 ms apart); off a wall, the void or a player it
/// comes straight back the way it came; it may leave the field and stop on other furni. Each
/// Banzai tile it lands on is one step of the kicker's team, sent on the room's next 500 ms beat,
/// and so is the tile it was kicked from. Its own state stays 0.
/// Inference: it goes 5 tiles a kick (two kicks went 5, the third 4) at an even 150 ms, and a
/// diagonal kick that meets a wall comes back diagonally (the one capture slid along it instead).
/// </summary>
[RoomObjectLogic(LOGIC_NAME)]
public class FurnitureBattleBanzaiPuckLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public const string LOGIC_NAME = "battlebanzai_puck";
    public const string CLASSNAME = "bb_puck";

    public const int KICK_TILES = 5;
    public const int MOVE_MS = 150;

    private RoomObjectId _kickerId;
    private (int X, int Y) _direction;
    private int _tilesLeft;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override async Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOnAsync(ctx, ct);

        if (!AvatarModule.TryGetAvatar(ctx.ObjectId, out var avatar))
            return;

        // The step onto the puck is starting: the avatar still stands on the tile it comes from.
        var direction = (
            Math.Sign(_ctx.RoomObject.X - avatar.X),
            Math.Sign(_ctx.RoomObject.Y - avatar.Y)
        );

        if (direction == (0, 0))
            direction = MapModule.GetDirectionOffset(avatar.Rotation);

        if (direction == (0, 0))
            return;

        _kickerId = ctx.ObjectId;
        _direction = direction;
        _tilesLeft = KICK_TILES;

        // The tile it was kicked from went with the kicker's step (t 1466.06, 1500.05).
        await StepTileAsync(ct);
        await GameSystem.SendGameFurniAsync(ct);

        TimerSystem.Schedule(_ctx.ObjectId, MOVE_MS, MoveAsync);
    }

    private async Task MoveAsync(CancellationToken ct)
    {
        if (_tilesLeft <= 0)
            return;

        _tilesLeft--;

        if (!CanGo(_direction))
            _direction = (-_direction.X, -_direction.Y);

        if (!CanGo(_direction))
        {
            _tilesLeft = 0;

            return;
        }

        var (x, y) = (_ctx.RoomObject.X + _direction.X, _ctx.RoomObject.Y + _direction.Y);

        if (
            !await FurniModule.MoveFloorItemAsync(
                ActionContext.System,
                (IRoomFloorItem)_ctx.RoomObject,
                MapModule.ToIdx(x, y),
                null,
                null,
                announce: true,
                ct
            )
        )
        {
            _tilesLeft = 0;

            return;
        }

        await StepTileAsync(ct);

        GameSystem.SendGameFurniOnBeat();

        if (_tilesLeft > 0)
            TimerSystem.Schedule(_ctx.ObjectId, MOVE_MS, MoveAsync);
    }

    private bool CanGo((int X, int Y) direction)
    {
        var (x, y) = (_ctx.RoomObject.X + direction.X, _ctx.RoomObject.Y + direction.Y);

        // It rolls over the field, whose tiles take nothing stacked on them (bb_patch1 is not
        // stackable).
        return FurniModule.CanPlaceFloorItem(
                (IRoomFloorItem)_ctx.RoomObject,
                x,
                y,
                _ctx.RoomObject.Rotation,
                item => item.Logic is FurnitureBattleBanzaiTileLogic
            ) && !AvatarModule.GetAvatarsOnTile(MapModule.ToIdx(x, y)).Any();
    }

    /// <summary>The kicker's step on the Banzai tile under the puck, if there is one.</summary>
    private async Task StepTileAsync(CancellationToken ct)
    {
        var tile = FurniModule
            .Items.OfType<IRoomFloorItem>()
            .Where(item =>
                item.X == _ctx.RoomObject.X
                && item.Y == _ctx.RoomObject.Y
                && item.Logic is FurnitureBattleBanzaiTileLogic
            )
            .Select(item => (FurnitureBattleBanzaiTileLogic)item.Logic)
            .FirstOrDefault();

        if (tile is null)
            return;

        await tile.StepAsync(_kickerId, ct);
    }
}
