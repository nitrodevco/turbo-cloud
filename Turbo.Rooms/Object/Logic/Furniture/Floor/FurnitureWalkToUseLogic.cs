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
    protected Task WalkToUseAsync(
        ActionContext ctx,
        IRoomAvatar avatar,
        IEnumerable<int> useTiles,
        CancellationToken ct
    ) =>
        WalkToAsync(
            avatar,
            useTiles,
            (arrived, token) => UseFromTileAsync(ctx, arrived, token),
            ct
        );

    /// <summary>
    /// Runs <paramref name="onArrived"/> for the avatar at once when it stands still on the first
    /// of <paramref name="tiles"/> (best first), or walks it to the first of them it can reach and
    /// runs it on arrival, in a tick of this furni. False when it can reach none of them. A later
    /// walk for the same avatar replaces this one; walking off elsewhere or taking too long
    /// forgets it.
    /// </summary>
    protected async Task<bool> WalkToAsync(
        IRoomAvatar avatar,
        IEnumerable<int> tiles,
        Func<IRoomAvatar, CancellationToken, Task> onArrived,
        CancellationToken ct
    )
    {
        var map = MapModule;
        var list = tiles.ToList();

        if (list.Count == 0)
            return false;

        if (map.ToIdx(avatar.X, avatar.Y) == list[0] && !avatar.IsWalking)
        {
            _approaching.Remove(avatar.ObjectId);

            await onArrived(avatar, ct);

            return true;
        }

        foreach (var tileIdx in list)
        {
            var (x, y) = map.GetTileXY(tileIdx);

            if (!await AvatarModule.WalkAvatarToAsync(avatar, x, y, ct))
                continue;

            _approaching[avatar.ObjectId] = new()
            {
                Arrive = onArrived,
                TileIdx = tileIdx,
                UntilMs = NowMs() + WalkTimeoutMs,
            };
            ScheduleTick();

            return true;
        }

        return false;
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
        var tickMs = RoomConfig.AvatarTickMs;

        if (UseAfterLastStepMs < 0)
        {
            TimerSystem.Schedule(_ctx.ObjectId, tickMs, TickAsync);

            return;
        }

        var now = NowMs();
        var boundary = State.NextAvatarBoundaryMs;
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
        var now = NowMs();

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
                    await approach.Arrive(avatar, ct);
            }
            else if (arrived)
            {
                _approaching.Remove(objectId);

                await approach.Arrive(avatar, ct);
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
        /// <summary>What happens once the avatar is there: the use, for a use request.</summary>
        public required Func<IRoomAvatar, CancellationToken, Task> Arrive { get; init; }

        /// <summary>The tile the avatar was sent to.</summary>
        public required int TileIdx { get; init; }

        /// <summary>When it stops waiting for the avatar.</summary>
        public required long UntilMs { get; init; }

        /// <summary>When the use happens, once the avatar has taken its last step; 0 before.</summary>
        public long UseAtMs { get; set; }
    }
}
