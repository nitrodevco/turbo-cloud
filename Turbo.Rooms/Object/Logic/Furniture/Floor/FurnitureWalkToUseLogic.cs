using System;
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
    /// <summary>Avatars on their way to use it, by avatar.</summary>
    private readonly Dictionary<RoomObjectId, Approach> _approaching = [];

    protected FurnitureWalkToUseLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
        : base(stuffDataFactory, ctx) { }

    /// <summary>How long it waits for an avatar it sent walking over to arrive.</summary>
    protected abstract int WalkTimeoutMs { get; }

    /// <summary>
    /// How long after the avatar's last step onto the tile it was sent to the use happens, at most
    /// one avatar tick; -1 waits until the avatar has stopped there.
    /// </summary>
    protected virtual int UseAfterLastStepMs => -1;

    /// <summary>
    /// Uses it for the avatar, which stands on the tile it was used from (or is taking its last
    /// step onto it); <paramref name="ctx"/> is the use request's.
    /// </summary>
    protected abstract Task UseFromTileAsync(
        ActionContext ctx,
        IRoomAvatar avatar,
        CancellationToken ct
    );

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
        ActionContext ctx,
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
            await UseFromTileAsync(ctx, avatar, ct);

            return;
        }

        foreach (var tileIdx in tiles)
        {
            var (x, y) = map.GetTileXY(tileIdx);

            if (!await AvatarModule.WalkAvatarToAsync(avatar, x, y, ct))
                continue;

            _approaching[avatar.ObjectId] = new()
            {
                Ctx = ctx,
                TileIdx = tileIdx,
                UntilMs = _roomGrain.NowMs() + WalkTimeoutMs,
            };
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

    /// <summary>
    /// Schedules its next tick. One that uses a while after the last step ticks right after the
    /// avatars step, on their boundary, so it sees that step in the tick it is taken, and again
    /// when a use it is waiting on is due.
    /// </summary>
    protected void ScheduleTick()
    {
        var tickMs = _roomGrain._roomConfig.AvatarTickMs;

        if (UseAfterLastStepMs < 0)
        {
            TimerSystem.Schedule(_ctx.ObjectId, tickMs, TickAsync);

            return;
        }

        var now = _roomGrain.NowMs();
        var boundary = _roomGrain._state.NextAvatarBoundaryMs;
        var dueAtMs = boundary > now ? boundary : now + tickMs;

        foreach (var approach in _approaching.Values)
        {
            if (approach.UseAtMs > 0)
                dueAtMs = Math.Min(dueAtMs, approach.UseAtMs);
        }

        TimerSystem.ScheduleAt(_ctx.ObjectId, dueAtMs, TickAsync);
    }

    /// <summary>
    /// Uses it for whoever has arrived, forgets whoever went elsewhere or took too long, and runs
    /// its own timed work; runs while any of that is pending.
    /// </summary>
    private async Task TickAsync(CancellationToken ct)
    {
        var now = _roomGrain.NowMs();

        foreach (var (objectId, approach) in _approaching.ToArray())
        {
            if (!AvatarModule.TryGetAvatar(objectId, out var avatar))
            {
                _approaching.Remove(objectId);

                continue;
            }

            var tileIdx = approach.TileIdx;
            var arrived = MapModule.ToIdx(avatar.X, avatar.Y) == tileIdx && !avatar.IsWalking;

            // The last step, once taken, stands: a walk asked for after it goes on from the tile.
            if (
                approach.UseAtMs == 0
                && !arrived
                && UseAfterLastStepMs >= 0
                && avatar.NextTileId == tileIdx
                && avatar.TilePath.Count == 0
                && avatar.GoalTileId == tileIdx
            )
                approach.UseAtMs = now + UseAfterLastStepMs;

            if (approach.UseAtMs > 0 && !arrived)
            {
                if (now < approach.UseAtMs)
                    continue;

                _approaching.Remove(objectId);

                if (avatar.NextTileId == tileIdx || MapModule.ToIdx(avatar.X, avatar.Y) == tileIdx)
                    await UseFromTileAsync(approach.Ctx, avatar, ct);
            }
            else if (arrived)
            {
                _approaching.Remove(objectId);

                await UseFromTileAsync(approach.Ctx, avatar, ct);
            }
            else if (!avatar.IsWalking || now >= approach.UntilMs || avatar.GoalTileId != tileIdx)
                _approaching.Remove(objectId);
        }

        var more = await TickMoreAsync(now, ct);

        if (_approaching.Count > 0 || more)
            ScheduleTick();
    }

    private sealed class Approach
    {
        /// <summary>The use request.</summary>
        public required ActionContext Ctx { get; init; }

        /// <summary>The tile the avatar was sent to.</summary>
        public required int TileIdx { get; init; }

        /// <summary>When it stops waiting for the avatar.</summary>
        public required long UntilMs { get; init; }

        /// <summary>When the use happens, once the avatar has taken its last step; 0 before.</summary>
        public long UseAtMs { get; set; }
    }
}
