using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Turbo.LoadBots.Knowledge;
using Turbo.LoadBots.Protocol.Decoders;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.LoadBots.Client;

/// <summary>
/// What a bot can see of the room it is in, kept current by the room messages as they arrive.
/// The receive loop writes and the bot's own task reads, so every member takes the lock.
/// </summary>
public sealed class RoomView(int roomId, int selfPlayerId)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, RoomAvatar> _avatars = [];
    private readonly Dictionary<int, AvatarStatus> _statuses = [];
    private readonly Dictionary<int, FloorItem> _items = [];
    private readonly List<ChatLine> _chat = [];
    private HeightMap? _map;
    private FloorPlan? _plan;

    private const int MAX_CHAT_LINES = 200;

    public int RoomId { get; } = roomId;
    public bool IsOwner { get; private set; }
    public bool Entered { get; private set; }

    public int SelfObjectId
    {
        get
        {
            lock (_lock)
                return _avatars
                        .Values.FirstOrDefault(x =>
                            x.WebId == selfPlayerId && x.Type is RoomObjectType.Player
                        )
                        ?.ObjectId
                    ?? -1;
        }
    }

    public HeightMap? Map
    {
        get
        {
            lock (_lock)
                return _map;
        }
    }

    public FloorPlan? Plan
    {
        get
        {
            lock (_lock)
                return _plan;
        }
    }

    public void SetEntered(bool isOwner)
    {
        lock (_lock)
        {
            Entered = true;
            IsOwner = isOwner;
        }
    }

    public void SetMap(HeightMap map)
    {
        lock (_lock)
            _map = map;
    }

    public void UpdateMap(IEnumerable<(int X, int Y, short Height)> tiles)
    {
        lock (_lock)
        {
            if (_map is null)
                return;

            foreach (var (x, y, height) in tiles)
            {
                if (x < _map.Width && y < _map.Length)
                    _map.Heights[y * _map.Width + x] = height;
            }
        }
    }

    public void SetPlan(FloorPlan plan)
    {
        lock (_lock)
            _plan = plan;
    }

    public void AddAvatars(IEnumerable<RoomAvatar> avatars)
    {
        lock (_lock)
        {
            foreach (var avatar in avatars)
                _avatars[avatar.ObjectId] = avatar;
        }
    }

    public void RemoveAvatar(int objectId)
    {
        lock (_lock)
        {
            _avatars.Remove(objectId);
            _statuses.Remove(objectId);
        }
    }

    public void UpdateStatuses(IEnumerable<AvatarStatus> statuses)
    {
        lock (_lock)
        {
            foreach (var status in statuses)
                _statuses[status.ObjectId] = status;
        }
    }

    public void SetItems(IEnumerable<FloorItem> items)
    {
        lock (_lock)
        {
            _items.Clear();

            foreach (var item in items)
                _items[item.ObjectId] = item;
        }
    }

    public void UpsertItem(FloorItem item)
    {
        lock (_lock)
            _items[item.ObjectId] = item;
    }

    public void RemoveItem(int objectId)
    {
        lock (_lock)
            _items.Remove(objectId);
    }

    public void AddChat(ChatLine line)
    {
        lock (_lock)
        {
            _chat.Add(line);

            if (_chat.Count > MAX_CHAT_LINES)
                _chat.RemoveRange(0, _chat.Count - MAX_CHAT_LINES);
        }
    }

    public IReadOnlyList<RoomAvatar> Avatars()
    {
        lock (_lock)
            return [.. _avatars.Values];
    }

    public IReadOnlyList<FloorItem> Items()
    {
        lock (_lock)
            return [.. _items.Values];
    }

    public FloorItem? Item(int objectId)
    {
        lock (_lock)
            return _items.GetValueOrDefault(objectId);
    }

    /// <summary>Whether an avatar other than <paramref name="selfObjectId"/> stands on the tile.</summary>
    public bool IsTakenByOther(int x, int y, int selfObjectId)
    {
        lock (_lock)
        {
            foreach (var (objectId, avatar) in _avatars)
            {
                if (objectId == selfObjectId)
                    continue;

                var (atX, atY) = _statuses.TryGetValue(objectId, out var moved)
                    ? (moved.X, moved.Y)
                    : (avatar.X, avatar.Y);

                if (atX == x && atY == y)
                    return true;
            }

            return false;
        }
    }

    public (int X, int Y)? Position(int objectId)
    {
        lock (_lock)
        {
            if (_statuses.TryGetValue(objectId, out var status))
                return (status.X, status.Y);

            return _avatars.TryGetValue(objectId, out var avatar) ? (avatar.X, avatar.Y) : null;
        }
    }

    /// <summary>
    /// Tiles a floor item of this footprint could go on without overlapping any furni the bot
    /// can see, inside the walkable floor and away from the door.
    /// </summary>
    public List<(int X, int Y)> FreeSpots(
        int width,
        int length,
        HotelKnowledge knowledge,
        (int X, int Y)? keepClear
    )
    {
        lock (_lock)
        {
            if (_map is null)
                return [];

            var occupied = OccupiedTiles(knowledge);
            var spots = new List<(int, int)>();

            for (var y = 0; y < _map.Length; y++)
            {
                for (var x = 0; x < _map.Width; x++)
                {
                    if (Fits(x, y, width, length, occupied, keepClear))
                        spots.Add((x, y));
                }
            }

            return spots;
        }
    }

    /// <summary>
    /// Tiles the avatar <paramref name="selfObjectId"/> can walk to from where it stands, by the
    /// server's rules (<c>RoomMapModule.CanAvatarWalk</c>): a seat or bed can end a walk but is
    /// never walked through, a diagonal step needs one of its two sides open, and a tile another
    /// avatar stands on is passed but never a goal. Picking from these keeps a walk the room
    /// cannot allow out of the walking checks.
    /// </summary>
    public List<(int X, int Y)> ReachableTiles(HotelKnowledge knowledge, int selfObjectId)
    {
        lock (_lock)
        {
            if (_map is null)
                return [];

            var from =
                _statuses.TryGetValue(selfObjectId, out var status) ? (status.X, status.Y)
                : _avatars.TryGetValue(selfObjectId, out var avatar) ? (avatar.X, avatar.Y)
                : ((int, int)?)null;

            if (from is not { } start)
                return [];

            var kinds = TileKinds(knowledge);
            var avatars = new HashSet<(int, int)>();

            foreach (var (objectId, other) in _avatars)
            {
                if (objectId != selfObjectId)
                    avatars.Add(
                        _statuses.TryGetValue(objectId, out var moved)
                            ? (moved.X, moved.Y)
                            : (other.X, other.Y)
                    );
            }

            TileKind KindOf(int x, int y) =>
                !_map.IsTile(x, y) ? TileKind.Blocked
                : kinds.TryGetValue((x, y), out var kind) ? kind
                : TileKind.Open;

            // A diagonal's side is checked as a goal would be: open, and nobody standing on it.
            bool SideOpen(int x, int y) =>
                KindOf(x, y) == TileKind.Open && !avatars.Contains((x, y));

            var visited = new HashSet<(int, int)> { start };
            var reachable = new HashSet<(int, int)>();
            var queue = new Queue<(int X, int Y)>();

            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var next = (X: x + dx, Y: y + dy);

                        if ((dx == 0 && dy == 0) || visited.Contains(next))
                            continue;

                        var kind = KindOf(next.X, next.Y);

                        if (kind == TileKind.Blocked)
                            continue;

                        if (dx != 0 && dy != 0 && !SideOpen(next.X, y) && !SideOpen(x, next.Y))
                            continue;

                        var occupied = avatars.Contains(next);

                        if (!occupied)
                            reachable.Add(next);

                        // A seat is only ever the last step; the search goes no further from it.
                        if (kind == TileKind.Open)
                        {
                            visited.Add(next);
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            return [.. reachable];
        }
    }

    private enum TileKind
    {
        Open,
        GoalOnly,
        Blocked,
    }

    /// <summary>
    /// What the furni on each covered tile makes it for an avatar. Solid furni blocks a tile
    /// outright; a seat or bed is somewhere to stop but not to pass.
    /// </summary>
    private Dictionary<(int, int), TileKind> TileKinds(HotelKnowledge knowledge)
    {
        var kinds = new Dictionary<(int, int), TileKind>();

        foreach (var item in _items.Values)
        {
            var definition = knowledge.FloorDefinition(item.SpriteId);
            var kind =
                definition is { CanSit: true } or { CanLay: true } ? TileKind.GoalOnly
                : definition is { CanWalk: true } ? TileKind.Open
                : TileKind.Blocked;
            var (width, length) = Footprint(definition, item.Rotation);

            for (var dy = 0; dy < length; dy++)
            {
                for (var dx = 0; dx < width; dx++)
                {
                    var tile = (item.X + dx, item.Y + dy);

                    // The most restrictive furni on a tile decides it.
                    if (!kinds.TryGetValue(tile, out var current) || kind > current)
                        kinds[tile] = kind;
                }
            }
        }

        return kinds;
    }

    private bool Fits(
        int x,
        int y,
        int width,
        int length,
        HashSet<(int, int)> occupied,
        (int X, int Y)? keepClear
    )
    {
        for (var dy = 0; dy < length; dy++)
        {
            for (var dx = 0; dx < width; dx++)
            {
                var tx = x + dx;
                var ty = y + dy;

                if (!_map!.IsTile(tx, ty) || occupied.Contains((tx, ty)))
                    return false;

                if (
                    keepClear is { } clear
                    && Math.Abs(clear.X - tx) <= 1
                    && Math.Abs(clear.Y - ty) <= 1
                )
                    return false;
            }
        }

        return true;
    }

    private HashSet<(int, int)> OccupiedTiles(HotelKnowledge knowledge)
    {
        var occupied = new HashSet<(int, int)>();

        foreach (var item in _items.Values)
        {
            var definition = knowledge.FloorDefinition(item.SpriteId);
            var (width, length) = Footprint(definition, item.Rotation);

            for (var dy = 0; dy < length; dy++)
            {
                for (var dx = 0; dx < width; dx++)
                    occupied.Add((item.X + dx, item.Y + dy));
            }
        }

        return occupied;
    }

    /// <summary>A furni's footprint: turned a quarter, its width and length swap.</summary>
    public static (int Width, int Length) Footprint(FurniDefinition? definition, int rotation)
    {
        var width = Math.Max(1, definition?.Width ?? 1);
        var length = Math.Max(1, definition?.Length ?? 1);

        return rotation is 2 or 6 ? (length, width) : (width, length);
    }
}
