using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomMapModule(RoomGrain roomGrain)
    : RoomGrainComponent(roomGrain),
        IRoomTileView
{
    /// <summary>
    /// What a tile's highest-item slot holds when no furni stands there. Zero, never -1:
    /// temporary furni count their ids down from -1, and with -1 as "none" every empty tile
    /// named the first temporary furni as its top item.
    /// </summary>
    private const int NO_ITEM = 0;

    private RoomMapSnapshot? _mapSnapshot = null;
    private bool _dirty = true;

    public int Width => State.Model?.Width ?? 0;
    public int Height => State.Model?.Height ?? 0;
    public int Size => State.Model?.Size ?? 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToIdx(int x, int y) => y * Width + x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetX(int idx) => idx % Width;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetY(int idx) => idx / Width;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetDistanceBetween(int a, int b) =>
        Math.Abs((a % Width) - (b % Width)) + Math.Abs(a / Width - b / Width);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(int idx) => (uint)idx < (uint)(Width * Height);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDiagonal(int idxA, int idxB)
    {
        var delta = idxB - idxA;

        if (delta != Width + 1 && delta != Width - 1 && delta != -Width + 1 && delta != -Width - 1)
            return false;

        var ax = idxA % Width;
        var bx = idxB % Width;

        return Math.Abs(ax - bx) == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int dx, int dy) GetDirectionOffset(Rotation dir)
    {
        return dir switch
        {
            Rotation.North => (0, -1),
            Rotation.NorthEast => (1, -1),
            Rotation.East => (1, 0),
            Rotation.SouthEast => (1, 1),
            Rotation.South => (0, 1),
            Rotation.SouthWest => (-1, 1),
            Rotation.West => (-1, 0),
            Rotation.NorthWest => (-1, -1),
            _ => (0, 0),
        };
    }

    /// <summary>A tile that is no part of the room: a hole in the model, or outside it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTileDisabled(int tileIdx) =>
        !InBounds(tileIdx) || State.TileFlags[tileIdx].Has(RoomTileFlags.Disabled);

    /// <summary>
    /// The furni on top of a tile, the one an avatar there stands on. None off the map. The one
    /// read of the top-item slot, so nothing else has to know what "none" is stored as.
    /// </summary>
    public bool TryGetHighestFloorItem(int tileIdx, [NotNullWhen(true)] out IRoomFloorItem? item)
    {
        item = null;

        if (
            !InBounds(tileIdx)
            || !State.ItemsById.TryGetValue(State.TileHighestFloorItems[tileIdx], out var found)
            || found is not IRoomFloorItem floorItem
        )
            return false;

        item = floorItem;

        return true;
    }

    /// <summary>How high the top of a tile is: what a furni put there lands on. Zero outside the room.</summary>
    public Altitude GetTileHeight(int tileIdx) =>
        InBounds(tileIdx) ? State.TileHeights[tileIdx] : Altitude.Zero;

    /// <summary>
    /// The neighbouring tiles that bring <paramref name="fromIdx"/> closer to
    /// <paramref name="toIdx"/>, straight steps only: the axis with more ground to cover first,
    /// the other one second. Empty when both are the same tile. Whether anything may stand on
    /// them is the caller's to check.
    /// </summary>
    public IEnumerable<int> GetStepsToward(int fromIdx, int toIdx) =>
        GetSteps(fromIdx, GetX(toIdx) - GetX(fromIdx), GetY(toIdx) - GetY(fromIdx));

    /// <summary>The same, leading away from <paramref name="awayFromIdx"/>.</summary>
    public IEnumerable<int> GetStepsAwayFrom(int fromIdx, int awayFromIdx) =>
        GetSteps(fromIdx, GetX(fromIdx) - GetX(awayFromIdx), GetY(fromIdx) - GetY(awayFromIdx));

    private IEnumerable<int> GetSteps(int fromIdx, int dx, int dy)
    {
        var (x, y) = (GetX(fromIdx), GetY(fromIdx));
        var alongX = (x + Math.Sign(dx), y);
        var alongY = (x, y + Math.Sign(dy));

        foreach (
            var (stepX, stepY) in Math.Abs(dx) >= Math.Abs(dy)
                ? new[] { alongX, alongY }
                : [alongY, alongX]
        )
        {
            if ((stepX != x || stepY != y) && InBounds(stepX, stepY))
                yield return ToIdx(stepX, stepY);
        }
    }

    public bool TryGetTileInFront(int index, Rotation direction, out int nextIndex)
    {
        var x = GetX(index);
        var y = GetY(index);

        var (dx, dy) = GetDirectionOffset(direction);

        var nx = x + dx;
        var ny = y + dy;

        if (!InBounds(nx, ny))
        {
            nextIndex = -1;

            return false;
        }

        nextIndex = ToIdx(nx, ny);

        return true;
    }

    public (int x, int y) GetTileXY(int idx)
    {
        if (!InBounds(idx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        return (GetX(idx), GetY(idx));
    }

    /// <summary>
    /// The tiles a floor item of this size covers at this spot and rotation. False, with no
    /// tiles, when any of them is off the map: that is an answer, not an error, so this never
    /// throws and a caller needs no bounds check of its own before asking.
    /// </summary>
    public bool GetTileIdForSize(
        int x,
        int y,
        Rotation rot,
        int width,
        int length,
        out List<int> tileIds
    ) => TryGetTileIds(FloorFootprint.Of(x, y, rot, width, length), out tileIds);

    /// <inheritdoc cref="GetTileIdForSize"/>
    public bool TryGetTileIds(FloorFootprint footprint, out List<int> tileIds)
    {
        tileIds = [];

        foreach (var (x, y) in footprint.Tiles())
        {
            // Checked by coordinate: an index past the right edge is a valid index on the next row.
            if (!InBounds(x, y))
            {
                tileIds = [];

                return false;
            }

            tileIds.Add(ToIdx(x, y));
        }

        return true;
    }

    public bool AddItem(IRoomItem item)
    {
        return item switch
        {
            IRoomFloorItem floor => AddFloorItem(floor),
            IRoomWallItem wall => AddWallItem(wall),
            _ => false,
        };
    }

    public bool RemoveItem(IRoomItem item)
    {
        return item switch
        {
            IRoomFloorItem floor => RemoveFloorItem(floor),
            IRoomWallItem wall => RemoveWallItem(wall),
            _ => false,
        };
    }

    /// <summary>
    /// A walk click on a tile with invisible click tiles on it is a click on each of them, which
    /// the "user clicks tile" trigger picks.
    /// </summary>
    public async Task ClickTileAsync(ActionContext ctx, int x, int y, CancellationToken ct)
    {
        if (!InBounds(x, y) || !State.TileFlags[ToIdx(x, y)].Has(RoomTileFlags.TileClickListener))
            return;

        foreach (var item in FurniModule.GetFloorItemsOnTile(ToIdx(x, y)))
        {
            if (item.Logic is not FurnitureInvisibleClickTileLogic)
                continue;

            await _roomGrain.PublishRoomEventAsync(
                new PlayerClickedTileEvent()
                {
                    PlayerId = ctx.PlayerId,
                    TileX = x,
                    TileY = y,
                    FurniId = item.ObjectId,
                    RoomId = _roomGrain.RoomId,
                    CausedBy = ctx,
                },
                ct
            );
        }
    }

    public void ComputeAllTiles()
    {
        FurniModule.UpdateAreaHiddenTiles();

        for (int idx = 0; idx < Size; idx++)
            ComputeTile(idx);
    }

    public void ComputeTile(int x, int y) => ComputeTile(ToIdx(x, y));

    public void ComputeTile(int id)
    {
        if (State.IsTileComputationPaused)
            return;

        foreach (var objectId in State.TileAvatarStacks[id])
        {
            if (AvatarModule.TryGetAvatar(objectId, out var avatar))
                avatar.NeedsInvoke = true;
        }

        var (nextHeight, nextFlags, nextHighestItem) = EvaluateTile(
            id,
            FurniModule.GetFloorItemsOnTile(id),
            static item => item.Z
        );

        State.TileHeights[id] = nextHeight;
        State.TileFlags[id] = nextFlags;
        State.TileHighestFloorItems[id] = nextHighestItem?.ObjectId ?? NO_ITEM;

        var prevEncoded = State.TileEncodedHeights[id];
        // A tile nothing may be stacked on shows the height its top furni stands at, not that
        // furni's top: a chair on the floor is 0, a bed on a 0.01 floor tile 0.01 (hhus, 2026-10-13).
        // A tile a switched-on area hider takes out of the floor is no tile to the client (official
        // client capture 2026-10-22: the area's tiles go -1 in the HeightMapUpdate after AreaHide
        // and in the HeightMap of a room entered with it on, and back when it is switched off).
        var nextEncoded = FurniModule.IsTileHiddenByArea(id)
            ? (short)-1
            : EncodeHeight(
                nextFlags.Has(RoomTileFlags.StackBlocked) && nextHighestItem is not null
                    ? nextHighestItem.Z
                    : nextHeight,
                nextFlags
            );

        if (prevEncoded != nextEncoded)
        {
            State.TileEncodedHeights[id] = nextEncoded;

            if (State.DirtyHeightTileIds.Add(id))
                State.DirtyHeightSentValues[id] = prevEncoded;
        }

        _dirty = true;
    }

    public RoomTileState GetTile(int tileIdx)
    {
        TryGetHighestFloorItem(tileIdx, out var highest);

        return new(State.TileHeights[tileIdx], State.TileFlags[tileIdx], highest);
    }

    /// <summary>
    /// What a tile is with these furni on it, each at the altitude <paramref name="zOf"/> gives:
    /// the one place the stacking rules live. <see cref="ComputeTile(int)"/> passes the furni
    /// standing there; a view of moves not yet made passes the furni it has put there.
    /// </summary>
    public RoomTileState EvaluateTile(
        int id,
        IEnumerable<IRoomFloorItem> floorItems,
        Func<IRoomFloorItem, Altitude> zOf
    )
    {
        Altitude nextHeight = State.Model?.BaseHeights[id] ?? 0.0;
        var nextFlags =
            State.Model?.BaseFlags[id] ?? (RoomTileFlags.Disabled | RoomTileFlags.Closed);

        IRoomFloorItem? nextHighestItem = null;
        IRoomFloorItem? stackHelper = null;
        Altitude stackHelperZ = 0.0;

        if (State.TileAvatarStacks[id].Count > 0)
            nextFlags = nextFlags.Add(RoomTileFlags.AvatarOccupied);

        foreach (var item in floorItems)
        {
            nextFlags = nextFlags.Add(RoomTileFlags.FurnitureOccupied);

            var z = zOf(item);
            Altitude height = z + item.GetStackHeight();

            if (item.Logic is FurnitureInvisibleClickTileLogic)
                nextFlags = nextFlags.Add(RoomTileFlags.TileClickListener);

            if (
                item.Logic is FurnitureStackTileLogic
                && (stackHelper is null || z.Value > stackHelperZ.Value)
            )
            {
                stackHelper = item;
                stackHelperZ = z;
            }

            if (height < nextHeight)
                continue;

            nextHeight = height;
            nextHighestItem = item;
        }

        // A stacking or walking helper decides its tiles' height whatever else stands there:
        // "Adjust the height of stacking helper as you want, then move the other furni on top of
        // it", "All users on this tile will stand on this height" (widget.custom.stack|walk.height.text).
        if (stackHelper is not null)
        {
            nextHeight = stackHelperZ + stackHelper.GetStackHeight();
            nextHighestItem = stackHelper;
        }

        if (nextHighestItem is not null)
        {
            if (!nextHighestItem.Logic.CanStack())
                nextFlags = nextFlags.Add(RoomTileFlags.StackBlocked);

            if (nextHighestItem.Logic.CanWalk())
                nextFlags = nextFlags.Add(RoomTileFlags.Walkable);

            if (nextHighestItem.Logic.CanSit())
                nextFlags = nextFlags.Add(RoomTileFlags.Sittable);

            if (nextHighestItem.Logic.CanLay())
                nextFlags = nextFlags.Add(RoomTileFlags.Layable);
        }

        return new(nextHeight, nextFlags, nextHighestItem);
    }

    public RoomMapSnapshot GetMapSnapshot(CancellationToken ct)
    {
        if (_dirty || _mapSnapshot is null)
        {
            _mapSnapshot = BuildSnapshot();
            _dirty = false;
        }

        return _mapSnapshot;
    }

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int x, int y, CancellationToken ct) =>
        GetTileSnapshotAsync(ToIdx(x, y), ct);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct) =>
        Task.FromResult(
            new RoomTileSnapshot
            {
                X = (byte)(id % (State.Model?.Width ?? 0)),
                Y = (byte)(id / (State.Model?.Width ?? 0)),
                Height = State.TileHeights[id],
                EncodedHeight = State.TileEncodedHeights[id],
                Flags = State.TileFlags[id],
                HighestObjectId = State.TileHighestFloorItems[id],
                FloorObjectIds = [.. State.TileFloorStacks[id]],
                AvatarObjectIds = [.. State.TileAvatarStacks[id]],
            }
        );

    private RoomMapSnapshot BuildSnapshot()
    {
        return new()
        {
            ModelName = State.Model?.Name ?? string.Empty,
            ModelData = State.Model?.Model ?? string.Empty,
            Width = State.Model?.Width ?? 0,
            Height = State.Model?.Height ?? 0,
            Size = State.Model?.Size ?? 0,
            DoorX = State.Model?.DoorX ?? 0,
            DoorY = State.Model?.DoorY ?? 0,
            DoorRotation = State.Model?.DoorRotation ?? 0,
            TileEncodedHeights = [.. State.TileEncodedHeights],
        };
    }

    /// <summary>
    /// A tile as the client's stacking height map reads it (<c>HeightMapMessageParser</c>): -1 for no
    /// tile at all, else the height × 256 in the bits below
    /// <see cref="Configuration.RoomConfig.StackingBlockedMaskBit"/>, with that bit set where nothing
    /// may be stacked (Habbo: bit 15, so a blocked floor tile is -32768).
    /// </summary>
    private short EncodeHeight(Altitude height, RoomTileFlags flags)
    {
        if (height < Altitude.Zero || flags.Has(RoomTileFlags.Disabled))
            return -1;

        int stackingMask = 1 << RoomConfig.StackingBlockedMaskBit;
        int heightMask = stackingMask - 1;
        // Truncated, as Habbo does: a 2.05 high stack is 524, not 525 (hhus, 2026-10-12).
        int raw = (int)(height.Value * 256.0);

        if (raw < 0)
            raw = 0;

        if (raw > heightMask)
            raw = heightMask;

        int value = raw;

        if (flags.Has(RoomTileFlags.StackBlocked))
            value |= stackingMask;

        // -1 means "no tile" to the client; a blocked tile at the very top of the range must not say that.
        var encoded = unchecked((short)(value & 0xFFFF));

        return encoded == -1 ? (short)-2 : encoded;
    }

    internal Task EnsureMapBuiltAsync(CancellationToken ct)
    {
        if (!State.IsMapReady)
        {
            var size = State.Model?.Size ?? 0;

            var tileHeights = new Altitude[size];
            var tileEncodedHeights = new short[size];
            var tileFlags = new RoomTileFlags[size];
            var tileHighestFloorItems = new RoomObjectId[size];
            var tileFloorStacks = new HashSet<RoomObjectId>[size];
            var tileAvatarStacks = new HashSet<RoomObjectId>[size];

            for (int id = 0; id < size; id++)
            {
                var height = State.Model?.BaseHeights[id] ?? Altitude.Zero;
                var flags =
                    State.Model?.BaseFlags[id]
                    ?? (RoomTileFlags.Disabled | RoomTileFlags.Closed | RoomTileFlags.StackBlocked);

                tileHeights[id] = height;
                tileEncodedHeights[id] = EncodeHeight(height, flags);
                tileFlags[id] = flags;
                tileHighestFloorItems[id] = NO_ITEM;
                tileFloorStacks[id] = [];
                tileAvatarStacks[id] = [];
            }

            State.TileHeights = tileHeights;
            State.TileEncodedHeights = tileEncodedHeights;
            State.TileFlags = tileFlags;
            State.TileHighestFloorItems = tileHighestFloorItems;
            State.TileFloorStacks = tileFloorStacks;
            State.TileAvatarStacks = tileAvatarStacks;
            State.IsMapReady = true;
        }

        return Task.CompletedTask;
    }
}
