using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Builders Club furni: furni the hotel lends a member for as long as their membership lasts.
/// It stands in the room like any other and has a row that outlives a reload, but nobody owns
/// it — it can never enter an inventory, and picking it up destroys it. What keeps it apart is
/// its id, which comes out of the band the client reads that out of
/// (<see cref="FurniIdBands"/>); the ids are handed out per room and stored with the row.
/// </summary>
public sealed partial class RoomFurniModule
{
    /// <summary>
    /// Borrows a floor item and stands it on a tile. False for every refusal; the client greys
    /// its own button out from the same rules, so a refusal here is logged with the ids rather
    /// than answered with an error the client has no dialog for.
    /// </summary>
    public async Task<bool> PlaceBuildersClubFloorItemAsync(
        ActionContext ctx,
        int pageId,
        int offerId,
        string extraParam,
        int x,
        int y,
        Rotation rot,
        bool confirmedHideRoom,
        CancellationToken ct
    )
    {
        var borrow = await PrepareBorrowAsync(ctx, offerId, ProductType.Floor, ct);

        if (borrow is not { } prepared)
            return false;

        if (
            !await MayBorrowWhileLapsedAsync(
                ctx,
                prepared,
                new BuildersClubPlacementWarningMessageComposer
                {
                    PlacementType = BuildersClubPlacementType.FloorItem,
                    PageId = pageId,
                    OfferId = offerId,
                    ExtraParam = extraParam,
                    X = x,
                    Y = y,
                    Direction = (int)rot,
                },
                confirmedHideRoom,
                ct
            )
        )
            return false;

        if (NextBuildersClubItemId() is not { } objectId)
            return false;

        if (
            _roomGrain._itemsLoader.CreateFromDefinition(
                objectId,
                ctx.PlayerId,
                prepared.Definition
            )
            is not IRoomFloorItem item
        )
            return false;

        if (!await PlaceFloorItemAsync(ctx, item, x, y, rot, ct))
            return false;

        return await CommitBorrowAsync(ctx, prepared, item, offerId, ct);
    }

    /// <summary>The wall counterpart of <see cref="PlaceBuildersClubFloorItemAsync"/>.</summary>
    public async Task<bool> PlaceBuildersClubWallItemAsync(
        ActionContext ctx,
        int pageId,
        int offerId,
        string extraParam,
        string location,
        WallPosition position,
        bool confirmedHideRoom,
        CancellationToken ct
    )
    {
        var borrow = await PrepareBorrowAsync(ctx, offerId, ProductType.Wall, ct);

        if (borrow is not { } prepared)
            return false;

        if (
            !await MayBorrowWhileLapsedAsync(
                ctx,
                prepared,
                new BuildersClubPlacementWarningMessageComposer
                {
                    PlacementType = BuildersClubPlacementType.WallItem,
                    PageId = pageId,
                    OfferId = offerId,
                    ExtraParam = extraParam,
                    WallLocation = location,
                },
                confirmedHideRoom,
                ct
            )
        )
            return false;

        if (NextBuildersClubItemId() is not { } objectId)
            return false;

        if (
            _roomGrain._itemsLoader.CreateFromDefinition(
                objectId,
                ctx.PlayerId,
                prepared.Definition
            )
            is not IRoomWallItem item
        )
            return false;

        if (
            !await PlaceWallItemAsync(
                ctx,
                item,
                position.X,
                position.Y,
                position.Z,
                position.WallOffset,
                position.Rotation,
                ct
            )
        )
            return false;

        return await CommitBorrowAsync(ctx, prepared, item, offerId, ct);
    }

    /// <summary>
    /// Everything that has to be true before a borrow, in the order the client checks it, so a
    /// refusal here means the client let through something it should have greyed out. Null when
    /// the borrow is refused.
    /// </summary>
    private async Task<BuildersClubBorrow?> PrepareBorrowAsync(
        ActionContext ctx,
        int offerId,
        ProductType productType,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return null;

        var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx);

        // The client refuses below its own controller level 3, which is a group admin here.
        if (controllerLevel < RoomControllerType.GroupAdmin)
            return Refuse(ctx, offerId, "they do not control the room");

        if (
            !_roomGrain.SecurityModule.IsRoomOwner(ctx.PlayerId)
            && await _roomGrain.GetIsGroupRoomAsync(ct)
            && !_roomGrain._roomConfig.BuildersClubInGroupRooms
        )
            return Refuse(ctx, offerId, "the hotel does not lend furni to group rooms");

        var subscription = await _roomGrain
            ._grainFactory.GetPlayerSubscriptionGrain(ctx.PlayerId)
            .GetAsync(SubscriptionType.BuildersClub, ct);

        var borrowedCount = await _roomGrain
            ._grainFactory.GetBuildersClubGrain()
            .GetBorrowedCountAsync(ctx.PlayerId, ct);

        if (borrowedCount >= subscription.FurniLimit)
        {
            _roomGrain._logger.LogWarning(
                "Player {PlayerId} may not borrow offer {OfferId} into room {RoomId}: they hold {BorrowedCount} of their {FurniLimit} furni",
                ctx.PlayerId,
                offerId,
                _roomGrain.RoomId,
                borrowedCount,
                subscription.FurniLimit
            );

            return null;
        }

        // The offer id is the only thing taken from the client, and only the Builders Club
        // catalog is asked, so nothing outside the warehouse can be borrowed.
        var catalog = _roomGrain._catalogService.GetCatalogSnapshot(CatalogType.BuildersClub);

        if (!catalog.OffersById.TryGetValue(offerId, out var offer) || !offer.Visible)
            return Refuse(ctx, offerId, "the warehouse has no such offer");

        var product = offer.Products.FirstOrDefault(x => x.ProductType == productType);

        if (
            product is null
            || _roomGrain._definitionProvider.TryGetDefinition(product.FurniDefinitionId)
                is not { } definition
        )
            return Refuse(ctx, offerId, $"it lends no {productType} furni");

        return new BuildersClubBorrow(definition, subscription.IsActive);
    }

    /// <summary>
    /// A member whose subscription has run out may still borrow, at the price of the room going
    /// invisible. The client is asked first and re-sends the same request with its confirmation,
    /// so an unconfirmed one is answered with the warning and nothing else. The room is not
    /// hidden here: that happens once the furni is actually standing in it
    /// (<see cref="CommitBorrowAsync"/>), so a placement that fails on a blocked tile does not
    /// take the room off the navigator for nothing.
    /// </summary>
    private async Task<bool> MayBorrowWhileLapsedAsync(
        ActionContext ctx,
        BuildersClubBorrow borrow,
        BuildersClubPlacementWarningMessageComposer warning,
        bool confirmedHideRoom,
        CancellationToken ct
    )
    {
        if (borrow.IsMember)
            return true;

        // A trial build is a private one. There are no staff ranks yet, so anyone else present
        // counts.
        if (_roomGrain.AvatarModule.Players.Any(x => x.PlayerId != ctx.PlayerId))
        {
            Refuse(ctx, warning.OfferId, "somebody else is in the room and they are on trial");

            return false;
        }

        // A room that is already off the navigator has nothing left to agree to.
        if (_roomGrain._state.RoomSnapshot.HiddenByBc || confirmedHideRoom)
            return true;

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(ctx.PlayerId, warning, ct);

        return false;
    }

    /// <summary>
    /// Writes the row behind a furni that is already standing in the room, counts the borrow and,
    /// for a lapsed member, takes the room off the navigator as they agreed. A write that fails
    /// takes the furni back out: a borrow the database never heard of would disappear on the next
    /// room load and still count against the borrower's limit.
    /// </summary>
    private async Task<bool> CommitBorrowAsync(
        ActionContext ctx,
        BuildersClubBorrow borrow,
        IRoomItem item,
        int offerId,
        CancellationToken ct
    )
    {
        var written = await _roomGrain
            ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
            .InsertBuildersClubItemAsync(_roomGrain.RoomId, item.GetSnapshot(), offerId, ct);

        if (!written)
        {
            // The removal counts no borrow back, because none was ever counted.
            await _roomGrain.ObjectModule.RemoveObjectAsync(ctx, item, ct, reportBorrow: false);

            return false;
        }

        await _roomGrain._grainFactory.GetBuildersClubGrain().OnBorrowedAsync(ctx.PlayerId, ct);

        if (!borrow.IsMember)
            await _roomGrain.SetHiddenByBuildersClubAsync(true, ct);

        return true;
    }

    /// <summary>
    /// The lowest free id of the Builders Club band in this room. Ids only have to be unique
    /// within a room, and a freed one is safe to hand out again because a borrowed furni takes
    /// its stored wired values with it when it leaves
    /// (<c>RoomWiredSystem.ForgetStoredValuesOfUnownedFurni</c>). Null when the room has somehow
    /// filled the band, which the room's own furni limits should reach first.
    /// </summary>
    private RoomObjectId? NextBuildersClubItemId()
    {
        var used = new HashSet<int>();

        foreach (var item in Items)
        {
            if (item.IsBuildersClub)
                used.Add(item.ObjectId.Value);
        }

        // The lowest free id is never further from the band's floor than the number of ids in
        // use, so this walks at most one more step than there are borrowed furni.
        for (
            var offset = 0;
            offset <= used.Count && offset < FurniIdBands.BuildersClubCapacity;
            offset++
        )
        {
            var objectId = FurniIdBands.BuildersClubMin + offset;

            if (!used.Contains(objectId))
                return (RoomObjectId)objectId;
        }

        _roomGrain._logger.LogError(
            "Room {RoomId} has no Builders Club id left to hand out",
            _roomGrain.RoomId
        );

        return null;
    }

    private BuildersClubBorrow? Refuse(ActionContext ctx, int offerId, string reason)
    {
        _roomGrain._logger.LogWarning(
            "Player {PlayerId} may not borrow offer {OfferId} into room {RoomId}: {Reason}",
            ctx.PlayerId,
            offerId,
            _roomGrain.RoomId,
            reason
        );

        return null;
    }

    /// <summary>What a borrow needs once every rule has passed.</summary>
    private readonly record struct BuildersClubBorrow(
        FurnitureDefinitionSnapshot Definition,
        bool IsMember
    );
}
