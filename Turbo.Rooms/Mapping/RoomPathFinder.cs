using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Mapping;

internal sealed class RoomPathfinder(int maxNodes = 4096)
{
    private static readonly int CARDINAL_COST = 10;
    private static readonly int DIAGONAL_COST = 14;
    private static readonly (int dx, int dy, int cost)[] DIRECTIONS =
    {
        (0, -1, 10), // N
        (1, -1, 14), // NE
        (1, 0, 10), // E
        (1, 1, 14), // SE
        (0, 1, 10), // S
        (-1, 1, 14), // SW
        (-1, 0, 10), // W
        (-1, -1, 14), // NW
    };

    private readonly int _maxNodes = maxNodes;

    public IReadOnlyList<(int X, int Y)> FindPath(
        IRoomAvatar avatar,
        IRoomMapViewer map,
        (int X, int Y) start,
        (int X, int Y) goal
    )
    {
        var (startX, startY) = start;
        var (goalX, goalY) = goal;
        var currentTileId = map.GetTileId(start.X, start.Y);
        var goalTileId = map.GetTileId(goal.X, goal.Y);

        if (
            currentTileId == goalTileId
            || !map.CanAvatarWalk(avatar, currentTileId)
            || !map.CanAvatarWalk(avatar, goalTileId)
        )
            return [];

        var open = new PriorityQueue<Node, int>();
        var allNodes = new Dictionary<(int, int), Node>(capacity: 256);

        Node GetOrCreateNode(int x, int y)
        {
            var key = (x, y);

            if (allNodes.TryGetValue(key, out var n))
                return n;

            n = new Node { X = x, Y = y };
            allNodes[key] = n;

            return n;
        }

        var startNode = GetOrCreateNode(startX, startY);

        startNode.G = 0;
        startNode.H = Heuristic(startX, startY, goalX, goalY);
        startNode.Parent = null;

        open.Enqueue(startNode, startNode.F);

        var closed = new HashSet<(int, int)>();

        while (open.Count > 0 && allNodes.Count <= _maxNodes)
        {
            try
            {
                var current = open.Dequeue();
                var cKey = (current.X, current.Y);
                var cTileId = map.GetTileId(current.X, current.Y);

                if (!closed.Add(cKey))
                    continue;

                if (current.X == goalX && current.Y == goalY)
                    return ReconstructPath(current);

                for (var i = 0; i < DIRECTIONS.Length; i++)
                {
                    var (dx, dy, moveCost) = DIRECTIONS[i];
                    var nx = current.X + dx;
                    var ny = current.Y + dy;

                    if (nx < 0 || ny < 0 || nx >= map.Width || ny >= map.Height)
                        continue;

                    if (closed.Contains((nx, ny)))
                        continue;

                    try
                    {
                        var nTileId = map.GetTileId(nx, ny);

                        if (!map.CanAvatarWalkBetween(avatar, cTileId, nTileId))
                            continue;

                        var tentativeG = current.G + moveCost;
                        var neighbor = GetOrCreateNode(nx, ny);

                        if (neighbor.Parent == null && !(nx == startX && ny == startY))
                        {
                            neighbor.Parent = current;
                            neighbor.G = tentativeG;
                            neighbor.H = Heuristic(nx, ny, goalX, goalY);
                            open.Enqueue(neighbor, neighbor.F);
                        }
                        else if (tentativeG < neighbor.G)
                        {
                            neighbor.Parent = current;
                            neighbor.G = tentativeG;
                            open.Enqueue(neighbor, neighbor.F);
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                continue;
            }
        }

        return [];
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

    private static List<(int X, int Y)> ReconstructPath(Node goalNode)
    {
        var list = new List<(int, int)>();
        var current = goalNode;

        while (current != null)
        {
            list.Add((current.X, current.Y));
            current = current.Parent!;
        }

        list.Reverse();

        return list;
    }
}
