using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms;

internal sealed partial class RoomService
{
    public async Task PlaceFloorItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (
            ctx.Origin != ActionOrigin.Player
            || ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || itemId <= 0
        )
            return;

        try
        {
            var inventoryGrain = _grainFactory.GetInventoryGrain(ctx.PlayerId);

            var snapshot = await inventoryGrain
                .GetItemSnapshotAsync(itemId, ct)
                .ConfigureAwait(false);

            if (snapshot is null || snapshot.Definition.ProductType != ProductType.Floor)
                return;

            if (
                !await PassesPlacementPolicyAsync(
                        new PlacementRequest(
                            ctx.PlayerId,
                            ctx.RoomId,
                            PlacementKind.FloorItem,
                            snapshot.Definition,
                            x,
                            y
                        ),
                        ct
                    )
                    .ConfigureAwait(false)
            )
                return;

            var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

            var result = await roomGrain
                .PlaceFloorItemAsync(ctx, snapshot, x, y, rot, ct)
                .ConfigureAwait(false);

            // A refused item stays in the inventory, and the player is told why.
            await SendPlacementErrorAsync(ctx.PlayerId, result, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to place floor item {ItemId} in room {RoomId} for player {PlayerId}",
                itemId,
                ctx.RoomId,
                ctx.PlayerId
            );
        }
    }

    public async Task MoveFloorItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        var result = await roomGrain
            .MoveFloorItemByIdAsync(ctx, itemId, x, y, rot, ct)
            .ConfigureAwait(false);

        if (result == FurniturePlacementResultType.Done)
            return;

        var item = await roomGrain.GetFloorItemSnapshotByIdAsync(itemId, ct).ConfigureAwait(false);

        if (item is null)
            return;

        // A refused move puts the furni back where the mover's client dragged it from.
        await _grainFactory
            .SendComposerToPlayerAsync(
                ctx.PlayerId,
                new ObjectUpdateMessageComposer { FloorItem = item },
                ct
            )
            .ConfigureAwait(false);

        await SendPlacementErrorAsync(ctx.PlayerId, result, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells a player the room would not take their furni where they put it - the hotel's
    /// <c>furni_placement_error</c> bubble, which the client shows for a refused placement or move
    /// of a floor or wall item alike: "Sorry, you cannot place this item here." for the spot,
    /// "You don't have rights to put furniture in here!" for a player who may not build. Habbo
    /// was seen answering a placement so; a refused move is told the same (inference).
    /// </summary>
    private Task SendPlacementErrorAsync(
        PlayerId playerId,
        FurniturePlacementResultType result,
        CancellationToken ct
    ) =>
        result == FurniturePlacementResultType.Done
            ? Task.CompletedTask
            : _grainFactory.SendComposerToPlayerAsync(
                playerId,
                new NotificationDialogMessageComposer
                {
                    NotificationType = FurniturePlacementNotifications.PLACEMENT_ERROR,
                    Parameters = ImmutableDictionary<string, string>.Empty.Add(
                        "message",
                        result switch
                        {
                            FurniturePlacementResultType.NotAllowed =>
                                FurniturePlacementNotifications.CANT_SET_NOT_OWNER_MESSAGE,
                            FurniturePlacementResultType.TypeLimit =>
                                FurniturePlacementNotifications.TYPE_LIMIT_MESSAGE,
                            _ => FurniturePlacementNotifications.CANT_SET_ITEM_MESSAGE,
                        }
                    ),
                },
                ct
            );
}
