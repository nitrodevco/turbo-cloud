using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A furni an avatar uses from a tile beside it. A use from such a tile happens at once; a use
/// from further away walks the avatar to one first, and the use happens once it is there. Anyone
/// who walks off somewhere else on the way, or takes too long, is forgotten.
/// </summary>
public abstract class FurnitureWalkToUseLogic : FurnitureFloorLogic
{
    /// <summary>Avatars on their way to use it: the tile they were sent to, and until when.</summary>
    private readonly Dictionary<RoomObjectId, (int TileIdx, long UntilMs)> _approaching = [];

    protected FurnitureWalkToUseLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
        : base(stuffDataFactory, ctx) { }

    /// <summary>How long it waits for an avatar it sent walking over to arrive.</summary>
    protected abstract int WalkTimeoutMs { get; }

    /// <summary>Uses it for the avatar, which stands on the tile it was used from.</summary>
    protected abstract Task UseFromTileAsync(IRoomAvatar avatar, CancellationToken ct);

    /// <summary>
    /// Its own timed work, run on each of its ticks after the arrivals: whether it needs another
    /// tick for it.
    /// </summary>
    protected virtual Task<bool> TickMoreAsync(long nowMs, CancellationToken ct) =>
        Task.FromResult(false);

    /// <summary>
    /// Uses it for the avatar from the first of <paramref name="useTiles"/> (best first) when it
    /// stands there, or walks it to the first of them it can reach and uses it on arrival.
    /// </summary>
    protected async Task WalkToUseAsync(
        IRoomAvatar avatar,
        IEnumerable<int> useTiles,
        CancellationToken ct
    )
    {
        var map = MapModule;
        var tiles = useTiles.ToList();

        if (tiles.Count == 0)
            return;

        if (map.ToIdx(avatar.X, avatar.Y) == tiles[0] && !avatar.IsWalking)
        {
            await UseFromTileAsync(avatar, ct);

            return;
        }

        foreach (var tileIdx in tiles)
        {
            var (x, y) = map.GetTileXY(tileIdx);

            if (!await AvatarModule.WalkAvatarToAsync(avatar, x, y, ct))
                continue;

            _approaching[avatar.ObjectId] = (tileIdx, _roomGrain.NowMs() + WalkTimeoutMs);
            ScheduleTick();

            return;
        }
    }

    /// <summary>
    /// The tiles touching the furni (diagonals included) an avatar may stand on, nearest to the
    /// avatar first. Tiles off the map or under other furni nobody may stand on are left out; a
    /// path is not asked for until the avatar is sent.
    /// </summary>
    protected IEnumerable<int> TilesBeside(IRoomAvatar avatar)
    {
        var map = MapModule;
        var footprint = FloorFootprint.Of(_ctx.RoomObject);

        return footprint
            .Tiles()
            .SelectMany(tile =>
                from dx in new[] { -1, 0, 1 }
                from dy in new[] { -1, 0, 1 }
                select (X: tile.X + dx, Y: tile.Y + dy)
            )
            .Distinct()
            .Where(tile =>
                map.InBounds(tile.X, tile.Y) && footprint.DistanceTo(tile.X, tile.Y) == 1
            )
            .Select(tile => (Idx: map.ToIdx(tile.X, tile.Y), tile.X, tile.Y))
            .Where(tile => map.CanAvatarWalk(avatar, tile.Idx))
            .OrderBy(tile =>
                (tile.X - avatar.X) * (tile.X - avatar.X)
                + (tile.Y - avatar.Y) * (tile.Y - avatar.Y)
            )
            .Select(tile => tile.Idx);
    }

    /// <summary>Whether the avatar stands still on a tile touching the furni.</summary>
    protected bool StandsBeside(IRoomAvatar avatar) =>
        !avatar.IsWalking && FloorFootprint.Of(_ctx.RoomObject).DistanceTo(avatar.X, avatar.Y) == 1;

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _approaching.Clear();

        return base.OnPickupAsync(ctx, ct);
    }

    protected void ScheduleTick() =>
        TimerSystem.Schedule(_ctx.ObjectId, _roomGrain._roomConfig.AvatarTickMs, TickAsync);

    /// <summary>
    /// Uses it for whoever has arrived, forgets whoever went elsewhere or took too long, and runs
    /// its own timed work; runs while any of that is pending.
    /// </summary>
    private async Task TickAsync(CancellationToken ct)
    {
        var now = _roomGrain.NowMs();

        foreach (var (objectId, (tileIdx, untilMs)) in _approaching.ToArray())
        {
            if (!AvatarModule.TryGetAvatar(objectId, out var avatar))
            {
                _approaching.Remove(objectId);

                continue;
            }

            var arrived = MapModule.ToIdx(avatar.X, avatar.Y) == tileIdx && !avatar.IsWalking;

            if (arrived)
            {
                _approaching.Remove(objectId);

                await UseFromTileAsync(avatar, ct);
            }
            else if (!avatar.IsWalking || now >= untilMs || avatar.GoalTileId != tileIdx)
                _approaching.Remove(objectId);
        }

        var more = await TickMoreAsync(now, ct);

        if (_approaching.Count > 0 || more)
            ScheduleTick();
    }
}
