using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> ApplyDecorationAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        // As in Habbo, a room's papers are its owner's to change (or staff controlling every
        // room); rights to move furni are not enough.
        if (!await SecurityModule.GetIsRoomOwnerAsync(ctx))
            return RejectDecoration(ctx, itemId, "not the room owner");

        var inventory = _grainFactory.GetInventoryGrain(ctx.PlayerId);
        var item = await inventory.GetItemSnapshotAsync(itemId, ct);

        if (item is null)
            return RejectDecoration(ctx, itemId, "not in their inventory");

        if (!RoomDecorations.TryGetPropertyType(item.Definition.FurniCategory, out var type))
            return RejectDecoration(ctx, itemId, "not a room paper");

        if (!ProductStuffData.TryGetValue(item.StuffData, out var pattern))
            return RejectDecoration(ctx, itemId, "the paper names no pattern");

        // Used up first: only what the inventory actually gave up is applied, so an item traded
        // or placed meanwhile cannot decorate the room for nothing.
        var consumed = await inventory.ConsumeFurnitureAsync(itemId, ct);

        if (consumed is null)
            return RejectDecoration(ctx, itemId, "the inventory no longer holds it");

        try
        {
            await SavePaintAsync(type.Value, pattern, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save {PropertyType} {Pattern} of room {RoomId}; giving item {ItemId} back to player {PlayerId}",
                type,
                pattern,
                _state.RoomId,
                itemId,
                ctx.PlayerId
            );

            // Nowhere else to put it: a failed give-back is logged with what was lost.
            try
            {
                await inventory.GrantFurnitureAsync(consumed.Definition.Id, consumed.ExtraData, ct);
            }
            catch (Exception grantEx)
            {
                _logger.LogError(
                    grantEx,
                    "Failed to give player {PlayerId} back definition {DefinitionId} with {ExtraData}",
                    ctx.PlayerId,
                    consumed.Definition.Id,
                    consumed.ExtraData
                );
            }

            return false;
        }

        _state.RoomProperties[type.Value] = pattern;

        await SendComposerToRoomAsync(
            new RoomPropertyMessageComposer
            {
                Key = RoomPropertyTypeExtensions.GetString(type.Value),
                Value = pattern,
            },
            ct
        );

        return true;
    }

    /// <summary>Writes one of the room's papers to its row.</summary>
    private async Task SavePaintAsync(RoomPropertyType type, string pattern, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var room = dbCtx.Rooms.Where(x => x.Id == _state.RoomId.Value);

        _ = type switch
        {
            RoomPropertyType.Wall => await room.ExecuteUpdateAsync(
                s => s.SetProperty(x => x.PaintWall, pattern),
                ct
            ),
            RoomPropertyType.Floor => await room.ExecuteUpdateAsync(
                s => s.SetProperty(x => x.PaintFloor, pattern),
                ct
            ),
            RoomPropertyType.Landscape => await room.ExecuteUpdateAsync(
                s => s.SetProperty(x => x.PaintLandscape, pattern),
                ct
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    /// <summary>Logs a refused paper with the ids that identify it, and yields false.</summary>
    private bool RejectDecoration(ActionContext ctx, RoomObjectId itemId, string reason)
    {
        _logger.LogWarning(
            "Rejected applying item {ItemId} to room {RoomId} by player {PlayerId}: {Reason}",
            itemId,
            _state.RoomId,
            ctx.PlayerId,
            reason
        );

        return false;
    }
}
