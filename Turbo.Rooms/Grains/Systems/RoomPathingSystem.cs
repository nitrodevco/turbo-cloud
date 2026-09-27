using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// A* over the room's tiles, by tile index. The per-tile bookkeeping lives in arrays kept for
/// the room's lifetime and sized to its map, and a search tells its own entries from older ones
/// by a stamp, so a search allocates nothing: every pet and bot in the room searches several
/// times a second. The queue, the costs and the order neighbours are tried in are those of the
/// node-object version this replaced, so the same room yields the same path.
/// </summary>
public sealed class RoomPathingSystem(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    private const int CARDINAL_COST = 10;
    private const int DIAGONAL_COST = 14;
    private const int NO_PARENT = -1;

    private static readonly (int dx, int dy, int cost)[] DIRECTIONS =
    {
        (0, -1, CARDINAL_COST), // N
        (1, -1, DIAGONAL_COST), // NE
        (1, 0, CARDINAL_COST), // E
        (1, 1, DIAGONAL_COST), // SE
        (0, 1, CARDINAL_COST), // S
        (-1, 1, DIAGONAL_COST), // SW
        (-1, 0, CARDINAL_COST), // W
        (-1, -1, DIAGONAL_COST), // NW
    };

    private readonly PriorityQueue<int, int> _open = new();

    private int[] _costFromStart = [];
    private int[] _costToGoal = [];
    private int[] _parent = [];

    // A tile was reached (or closed) by the current search when its entry equals _stamp.
    private int[] _reachedStamp = [];
    private int[] _closedStamp = [];
    private int _stamp;

    /// <summary>
    /// Finds a way from <paramref name="startIdx"/> to <paramref name="goalIdx"/>. On success
    /// <paramref name="path"/> holds the steps after the start, the goal first and the next step
    /// last, so a walk takes its next step off the end; on failure it is left as it was.
    /// </summary>
    public bool TryFindPath(IRoomAvatar avatar, int startIdx, int goalIdx, List<int> path)
    {
        var map = MapModule;

        if (
            startIdx == goalIdx
            || !map.CanAvatarWalk(avatar, startIdx)
            || !map.CanAvatarWalk(avatar, goalIdx)
            || !HasWayIn(avatar, startIdx, goalIdx)
        )
            return false;

        try
        {
            return Search(avatar, startIdx, goalIdx, path);
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Failed to find a path for avatar {ObjectId} from tile {From} to tile {To} in room {RoomId}",
                avatar.ObjectId,
                startIdx,
                goalIdx,
                _roomGrain.RoomId
            );

            return false;
        }
    }

    /// <summary>
    /// Whether any tile beside the goal is one the search could step onto the goal from: the
    /// start, or a tile an avatar may pass over, with the step itself allowed. Without one the
    /// search would visit everything reachable before giving up, which is what a walled-in or
    /// taken goal used to cost.
    /// </summary>
    private bool HasWayIn(IRoomAvatar avatar, int startIdx, int goalIdx)
    {
        var map = MapModule;
        var width = map.Width;
        var goalX = goalIdx % width;
        var goalY = goalIdx / width;

        foreach (var (dx, dy, _) in DIRECTIONS)
        {
            var x = goalX + dx;
            var y = goalY + dy;

            if (!map.InBounds(x, y))
                continue;

            var idx = map.ToIdx(x, y);

            if (
                (idx == startIdx || map.CanAvatarWalk(avatar, idx, isGoal: false))
                && map.CanAvatarWalkBetween(avatar, idx, goalIdx, isGoal: true)
            )
                return true;
        }

        return false;
    }

    private bool Search(IRoomAvatar avatar, int startIdx, int goalIdx, List<int> path)
    {
        var map = MapModule;
        var width = map.Width;
        var height = map.Height;
        var maxNodes = _roomGrain._roomConfig.MaxPathNodes;
        var goalX = goalIdx % width;
        var goalY = goalIdx / width;
        var stamp = BeginSearch(map.Size);

        _costFromStart[startIdx] = 0;
        _costToGoal[startIdx] = Heuristic(startIdx % width, startIdx / width, goalX, goalY);
        _parent[startIdx] = NO_PARENT;
        _reachedStamp[startIdx] = stamp;

        var reached = 1;

        _open.Enqueue(startIdx, _costToGoal[startIdx]);

        while (_open.Count > 0 && reached <= maxNodes)
        {
            var current = _open.Dequeue();

            if (_closedStamp[current] == stamp)
                continue;

            _closedStamp[current] = stamp;

            if (current == goalIdx)
            {
                path.Clear();

                for (var idx = goalIdx; idx != startIdx; idx = _parent[idx])
                    path.Add(idx);

                return true;
            }

            var currentX = current % width;
            var currentY = current / width;

            foreach (var (dx, dy, moveCost) in DIRECTIONS)
            {
                var x = currentX + dx;
                var y = currentY + dy;

                if ((uint)x >= (uint)width || (uint)y >= (uint)height)
                    continue;

                var next = y * width + x;

                if (_closedStamp[next] == stamp)
                    continue;

                if (!map.CanAvatarWalkBetween(avatar, current, next, next == goalIdx))
                    continue;

                var cost = _costFromStart[current] + moveCost;

                if (_reachedStamp[next] != stamp)
                {
                    _reachedStamp[next] = stamp;
                    reached++;

                    _parent[next] = current;
                    _costFromStart[next] = cost;
                    _costToGoal[next] = Heuristic(x, y, goalX, goalY);

                    _open.Enqueue(next, cost + _costToGoal[next]);
                }
                else if (cost < _costFromStart[next])
                {
                    _parent[next] = current;
                    _costFromStart[next] = cost;

                    _open.Enqueue(next, cost + _costToGoal[next]);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Readies the arrays for a map of <paramref name="size"/> tiles (a saved floor plan can
    /// change it) and returns the stamp the new search marks its tiles with.
    /// </summary>
    private int BeginSearch(int size)
    {
        _open.Clear();

        if (_parent.Length != size)
        {
            _costFromStart = new int[size];
            _costToGoal = new int[size];
            _parent = new int[size];
            _reachedStamp = new int[size];
            _closedStamp = new int[size];
            _stamp = 0;
        }

        if (_stamp == int.MaxValue)
        {
            Array.Clear(_reachedStamp);
            Array.Clear(_closedStamp);
            _stamp = 0;
        }

        return ++_stamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Heuristic(int x, int y, int goalX, int goalY)
    {
        var dx = Math.Abs(x - goalX);
        var dy = Math.Abs(y - goalY);

        return (dx < dy)
            ? DIAGONAL_COST * dx + CARDINAL_COST * (dy - dx)
            : DIAGONAL_COST * dy + CARDINAL_COST * (dx - dy);
    }
}
