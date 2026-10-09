using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Furni a switched-on Room Area Hider hides. The client only takes the floor out of the area
/// (Flash <c>RoomEngine.updateAreaHide</c>); the furni standing there are hidden by not being
/// sent: Habbo's hider hides an area "completely", is how builders keep their wired out of sight
/// and lag down, and was fixed for "floor furni not appearing after revealed by area hider", so
/// what it hides comes back when it is switched off. Everyone in the room is treated alike;
/// avatars are not hidden. A hidden item is left out of the entry lists, taken away when an
/// area starts to cover it and sent again when it stops, and placing or moving one keeps to the
/// same rule.
/// </summary>
public sealed partial class RoomFurniModule
{
    private List<FurnitureAreaHideLogic> GetActiveAreaHiders() =>
        [
            .. Items
                .Select(x => x.Logic)
                .OfType<FurnitureAreaHideLogic>()
                .Where(x => x.GetActiveArea() is not null),
        ];

    public bool IsHiddenByArea(IRoomItem item) => GetActiveAreaHiders().Any(x => x.Hides(item));

    /// <summary>Every item an area hides right now.</summary>
    public HashSet<RoomObjectId> GetHiddenItemIds()
    {
        var hiders = GetActiveAreaHiders();

        return hiders.Count == 0
            ? []
            : [.. Items.Where(item => hiders.Any(x => x.Hides(item))).Select(x => x.ObjectId)];
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
    /// After an area hider changed: takes away what is hidden now and was not, and sends again
    /// what was hidden and is not, as one batch.
    /// </summary>
    public Task AnnounceHiddenItemsChangedAsync(
        HashSet<RoomObjectId> hiddenBefore,
        CancellationToken ct
    )
    {
        var hiddenNow = GetHiddenItemIds();
        var composers = new List<IComposer>();

        foreach (var id in hiddenNow.Except(hiddenBefore))
        {
            if (TryGetItem(id, out var item))
                composers.Add(item.GetRemoveComposer(PlayerId.Invalid));
        }

        foreach (var id in hiddenBefore.Except(hiddenNow))
        {
            if (TryGetItem(id, out var item))
                composers.Add(item.GetAddComposer());
        }

        return _roomGrain.SendComposersToRoomAsync([.. composers], ct);
    }

    /// <summary>
    /// What the room is told of an item that moved: an update while it stays in sight, its
    /// removal when it moved into a hidden area, itself again when it moved out of one, and
    /// nothing while it stays hidden.
    /// </summary>
    private IComposer? GetMoveComposer(IRoomItem item, bool wasHidden)
    {
        var isHidden = IsHiddenByArea(item);

        return (wasHidden, isHidden) switch
        {
            (false, false) => item.GetUpdateComposer(),
            (false, true) => item.GetRemoveComposer(PlayerId.Invalid),
            (true, false) => item.GetAddComposer(),
            _ => null,
        };
    }
}
