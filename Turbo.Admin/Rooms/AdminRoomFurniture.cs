using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Admin.Rooms;

/// <summary>
/// The furniture in a room, for its page. Taking an item out goes through the room grain, as a
/// moderator picking it up in the room would: those in the room see it go, and it reaches its
/// owner's inventory.
/// </summary>
public sealed class AdminRoomFurniture(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory
)
{
    /// <summary>The furniture in the room, by classname.</summary>
    public async Task<List<RoomFurnitureItem>> ListAsync(int roomId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .Furnitures.AsNoTracking()
            .Where(x => x.RoomEntityId == roomId)
            .OrderBy(x => x.FurnitureDefinitionEntity!.Name)
            .ThenBy(x => x.Id)
            .Select(x => new RoomFurnitureItem(
                x.Id,
                x.FurnitureDefinitionEntityId,
                x.FurnitureDefinitionEntity!.Name,
                x.FurnitureDefinitionEntity.PublicName,
                x.FurnitureDefinitionEntity.SpriteId,
                x.FurnitureDefinitionEntity.ProductType == ProductType.Wall,
                x.FurnitureDefinitionEntity.Logic,
                x.PlayerEntityId,
                x.PlayerEntity!.Name,
                x.X,
                x.Y,
                x.Z,
                (int)x.Rotation,
                x.ExtraData
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>Takes an item out of the room, back to its owner's inventory; false when it doesn't stand there.</summary>
    public Task<bool> PickupAsync(int roomId, int itemId, PlayerId staff, CancellationToken ct) =>
        grainFactory.GetRoomGrain(new RoomId(roomId)).StaffPickupItemAsync(itemId, staff.Value, ct);
}
