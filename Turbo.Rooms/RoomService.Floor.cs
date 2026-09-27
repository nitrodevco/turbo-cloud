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

            var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

            if (
                !await roomGrain
                    .PlaceFloorItemAsync(ctx, snapshot, x, y, rot, ct)
                    .ConfigureAwait(false)
            )
            {
                // The spot does not take the item: it stays in the inventory, and the player is told.
                await SendPlacementErrorAsync(ctx.PlayerId, ct).ConfigureAwait(false);

                return;
            }
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

        if (
            await roomGrain.MoveFloorItemByIdAsync(ctx, itemId, x, y, rot, ct).ConfigureAwait(false)
        )
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

        await SendPlacementErrorAsync(ctx.PlayerId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells a player the room would not take their furni where they put it - the hotel's
    /// <c>furni_placement_error</c> bubble, "Sorry, you cannot place this item here.", which the
    /// client shows for a refused placement or move of a floor or wall item alike.
    /// </summary>
    private Task SendPlacementErrorAsync(PlayerId playerId, CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(
            playerId,
            new NotificationDialogMessageComposer
            {
                NotificationType = FurniturePlacementNotifications.PLACEMENT_ERROR,
                Parameters = ImmutableDictionary<string, string>.Empty.Add(
                    "message",
                    FurniturePlacementNotifications.CANT_SET_ITEM_MESSAGE
                ),
            },
            ct
        );
}
