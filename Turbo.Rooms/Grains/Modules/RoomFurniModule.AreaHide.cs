using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Furni a switched-on Room Area Hider hides. The client takes the floor out of the area (Flash
/// <c>RoomEngine.updateAreaHide</c>); the server takes away the furni standing there and sends
/// them again when it is switched off, for everyone in the room, the owner too. A hidden item is
/// left out of the entry lists, and placing or moving one keeps to the same rule. While an
/// Invisible Furni Controller is on, the room's invisible furni are taken away the same way: a
/// hider set to "Make this furniture invisible" and the furni <c>RoomConfig.InvisibleFurniNames</c>
/// lists (official client capture 2026-10-22: switching on sends one ObjectRemoveMultiple
/// {ids, picker -1, byInvisibleFurniManager} of everything it hides, switching off one Objects
/// with them; a room entered with either on leaves them out of Objects).
/// </summary>
public sealed partial class RoomFurniModule
{
    // Whether the room's invisible furni are hidden, as the clients were last told.
    private bool _invisibleFurniHidden;

    // The tiles the switched-on hiders take out of the floor, as the height map was last given them.
    private HashSet<int> _areaHiddenTiles = [];

    internal List<FurnitureAreaHideLogic> GetActiveAreaHiders() =>
        [
            .. Items
                .Select(x => x.Logic)
                .OfType<FurnitureAreaHideLogic>()
                .Where(x => x.GetActiveArea() is not null),
        ];

    public bool IsHidden(IRoomItem item) => IsHidden(item, GetActiveAreaHiders());

    /// <summary>
    /// Whether the item is out of sight: an area covers it, or it is an invisible furni while the
    /// controller is on.
    /// </summary>
    internal bool IsHidden(IRoomItem item, List<FurnitureAreaHideLogic> hiders) =>
        hiders.Any(x => x.Hides(item)) || _invisibleFurniHidden && IsInvisibleFurni(item);

    /// <summary>
    /// Whether the Invisible Furni Controller takes the item away: a hider set to "Make this
    /// furniture invisible" or a furni of a kind the hotel lists as invisible (Habbo:
    /// the seven invisible blocks and tiles, official client capture 2026-10-22).
    /// </summary>
    internal bool IsInvisibleFurni(IRoomItem item) =>
        item.Logic is FurnitureAreaHideLogic { IsInvisible: true }
        || RoomConfig.InvisibleFurniNames.Contains(item.Definition.Name);

    /// <summary>Whether a switched-on hider takes the tile out of the floor.</summary>
    public bool IsTileHiddenByArea(int tileIdx) => _areaHiddenTiles.Contains(tileIdx);

    /// <summary>
    /// After a hider changed, arrived or left: works out the tiles the hiders take out of the
    /// floor again and recomputes those that changed, whose heights the next flush sends (Habbo:
    /// a HeightMapUpdate ~120-230 ms after the AreaHide).
    /// </summary>
    public void RefreshAreaHiddenTiles()
    {
        foreach (var idx in UpdateAreaHiddenTiles().Order())
            MapModule.ComputeTile(idx);
    }

    /// <summary>Works out the tiles the hiders take out of the floor again; returns those that changed.</summary>
    internal HashSet<int> UpdateAreaHiddenTiles()
    {
        var hiders = GetActiveAreaHiders();
        var next = new HashSet<int>();

        if (hiders.Count > 0)
        {
            for (var idx = 0; idx < MapModule.Size; idx++)
            {
                var (x, y) = MapModule.GetTileXY(idx);

                if (hiders.Any(h => h.HidesTile(x, y)))
                    next.Add(idx);
            }
        }

        var changed = new HashSet<int>(_areaHiddenTiles);

        changed.SymmetricExceptWith(next);
        _areaHiddenTiles = next;

        return changed;
    }

    /// <summary>Every item hidden right now.</summary>
    public HashSet<RoomObjectId> GetHiddenItemIds()
    {
        var hiders = GetActiveAreaHiders();

        return hiders.Count == 0 && !_invisibleFurniHidden
            ? []
            : [.. Items.Where(item => IsHidden(item, hiders)).Select(x => x.ObjectId)];
    }

    /// <summary>
    /// After an Invisible Furni Controller changed, arrived or is leaving: takes the invisible
    /// furni away when one is on now and none was, and sends them again when none is left on.
    /// </summary>
    public Task RefreshInvisibleFurniAsync(RoomObjectId? leaving, CancellationToken ct)
    {
        var hidden =
            FurnitureConfigurationControlLogic.IsOnInRoom<FurnitureInvisibleFurniControlLogic>(
                this,
                leaving
            );

        if (hidden == _invisibleFurniHidden)
            return Task.CompletedTask;

        var hiddenBefore = GetHiddenItemIds();

        _invisibleFurniHidden = hidden;

        return AnnounceHiddenItemsChangedAsync(hiddenBefore, ct);
    }

    /// <summary>The floor items a player walking in is sent.</summary>
    public ImmutableArray<RoomFloorItemSnapshot> GetShownFloorItemSnapshots()
    {
        var hidden = GetHiddenItemIds();

        return
        [
            .. Items
                .OfType<IRoomFloorItem>()
                .Where(x => !hidden.Contains(x.ObjectId))
                .Select(x => x.GetSnapshot()),
        ];
    }

    /// <summary>The wall items a player walking in is sent.</summary>
    public ImmutableArray<RoomWallItemSnapshot> GetShownWallItemSnapshots()
    {
        var hidden = GetHiddenItemIds();

        return
        [
            .. Items
                .OfType<IRoomWallItem>()
                .Where(x => !hidden.Contains(x.ObjectId))
                .Select(x => x.GetSnapshot()),
        ];
    }

    /// <summary>
    /// After an area hider or the Invisible Furni Controller changed: takes away what is hidden
    /// now and was not, in one ObjectRemoveMultiple (and one ItemRemoveMultiple for wall items),
    /// and sends again what was hidden and is not, in one Objects (and one Items), as Habbo does.
    /// </summary>
    public Task AnnounceHiddenItemsChangedAsync(
        HashSet<RoomObjectId> hiddenBefore,
        CancellationToken ct
    ) => _roomGrain.SendComposersToRoomAsync([.. GetHiddenItemsChangedComposers(hiddenBefore)], ct);

    /// <inheritdoc cref="AnnounceHiddenItemsChangedAsync"/>
    internal List<IComposer> GetHiddenItemsChangedComposers(HashSet<RoomObjectId> hiddenBefore)
    {
        var hiddenNow = GetHiddenItemIds();
        var hiddenFloor = new List<long>();
        var hiddenWall = new List<long>();
        var shownFloor = new List<RoomFloorItemSnapshot>();
        var shownWall = new List<RoomWallItemSnapshot>();

        foreach (var id in hiddenNow.Except(hiddenBefore))
        {
            if (!TryGetItem(id, out var item))
                continue;

            (item is IRoomWallItem ? hiddenWall : hiddenFloor).Add(id.Value);
        }

        foreach (var id in hiddenBefore.Except(hiddenNow))
        {
            if (!TryGetItem(id, out var item))
                continue;

            if (item is IRoomFloorItem floor)
                shownFloor.Add(floor.GetSnapshot());
            else if (item is IRoomWallItem wall)
                shownWall.Add(wall.GetSnapshot());
        }

        var composers = new List<IComposer>();

        if (hiddenFloor.Count > 0)
            composers.Add(
                new ObjectRemoveMultipleMessageComposer
                {
                    ObjectIdsToRemove = [.. hiddenFloor],
                    PickerId = PlayerId.Invalid,
                    ByInvisibleFurniManager = true,
                }
            );

        if (hiddenWall.Count > 0)
            composers.Add(
                new ItemRemoveMultipleMessageComposer
                {
                    ObjectIdsToRemove = [.. hiddenWall],
                    PickerId = PlayerId.Invalid,
                    ByInvisibleFurniManager = true,
                }
            );

        if (shownFloor.Count > 0)
            composers.Add(
                new ObjectsMessageComposer
                {
                    OwnerNames = OwnerNamesOf(shownFloor.Select(x => (x.OwnerId, x.OwnerName))),
                    FloorItems = [.. shownFloor],
                }
            );

        if (shownWall.Count > 0)
            composers.Add(
                new ItemsMessageComposer
                {
                    OwnerNames = OwnerNamesOf(shownWall.Select(x => (x.OwnerId, x.OwnerName))),
                    WallItems = [.. shownWall],
                }
            );

        return composers;
    }

    private static ImmutableDictionary<PlayerId, string> OwnerNamesOf(
        IEnumerable<(PlayerId OwnerId, string OwnerName)> owners
    )
    {
        var names = ImmutableDictionary.CreateBuilder<PlayerId, string>();

        foreach (var (ownerId, ownerName) in owners)
            names[ownerId] = ownerName;

        return names.ToImmutable();
    }

    /// <summary>
    /// Wired moves follow the same rule as a player's: a furni that stays hidden is left out of
    /// the action's movement packet, one that moved into a hidden area is taken away and one
    /// that moved out of one is sent again. Returns what to send after the movements.
    /// </summary>
    internal List<IComposer> TakeHiddenWiredMoves(
        List<WiredFloorItemMovementSnapshot> floorMoves,
        List<WiredWallItemMovementSnapshot> wallMoves,
        IReadOnlyDictionary<RoomObjectId, bool> hiddenBefore
    )
    {
        var composers = new List<IComposer>();
        var hiders = GetActiveAreaHiders();

        if (hiders.Count == 0 && !_invisibleFurniHidden && !hiddenBefore.Values.Any(x => x))
            return composers;

        bool Shown(RoomObjectId id)
        {
            if (!TryGetItem(id, out var item))
                return true;

            var wasHidden = hiddenBefore.TryGetValue(id, out var before) && before;
            var isHidden = IsHidden(item, hiders);

            if (wasHidden == isHidden)
                return !isHidden;

            composers.Add(isHidden ? item.GetHideComposer() : item.GetAddComposer());

            return false;
        }

        floorMoves.RemoveAll(x => !Shown(x.ObjectId));
        wallMoves.RemoveAll(x => !Shown(x.ObjectId));

        return composers;
    }

    /// <summary>
    /// What the room is told of a floor furni that moved: as <see cref="GetMoveComposer"/>, with
    /// its update after the removal or the add when it crossed a hidden area's edge (official
    /// client capture 2026-10-22, MoveObject: into = ObjectRemove then ObjectUpdate 2/2, out of =
    /// ObjectAdd then ObjectUpdate 3/3, in the same millisecond).
    /// </summary>
    private IEnumerable<IComposer> GetFloorMoveComposers(IRoomItem item, bool wasHidden)
    {
        if (GetMoveComposer(item, wasHidden) is not { } moved)
            yield break;

        yield return moved;

        if (wasHidden != IsHidden(item))
            yield return item.GetUpdateComposer();
    }

    /// <summary>
    /// What the room is told of an item that moved: an update while it stays in sight, its
    /// removal when it moved into a hidden area, itself again when it moved out of one, and
    /// nothing while it stays hidden.
    /// </summary>
    private IComposer? GetMoveComposer(IRoomItem item, bool wasHidden)
    {
        var isHidden = IsHidden(item);

        return (wasHidden, isHidden) switch
        {
            (false, false) => item.GetUpdateComposer(),
            (false, true) => item.GetHideComposer(),
            (true, false) => item.GetAddComposer(),
            _ => null,
        };
    }
}
