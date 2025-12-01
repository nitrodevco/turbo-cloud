using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Object.Furniture;

internal sealed class RoomItemsLoader(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IFurnitureDefinitionProvider defsProvider
) : IRoomItemsLoader
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;

    public async Task<(
        IReadOnlyList<IRoomFloorItem>,
        IReadOnlyList<IRoomWallItem>
    )> LoadByRoomIdAsync(long roomId, CancellationToken ct)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .Furnitures.AsNoTracking()
                .Where(x => x.RoomEntityId == roomId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var floorItems = new List<IRoomFloorItem>();
            var wallItems = new List<IRoomWallItem>();

            foreach (var entity in entities)
            {
                try
                {
                    var item = CreateFromEntity(entity);

                    if (item is IRoomFloorItem floorItem)
                    {
                        floorItem.SetPosition(entity.X, entity.Y, entity.Z);
                        floorItem.SetRotation(entity.Rotation);

                        floorItems.Add(floorItem);
                    }

                    if (item is IRoomWallItem wallItem)
                    {
                        wallItems.Add(wallItem);
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return (floorItems, wallItems);
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    public IRoomItem CreateFromEntity(FurnitureEntity entity)
    {
        var definition =
            _defsProvider.TryGetDefinition(entity.FurnitureDefinitionEntityId)
            ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);

        return definition.ProductType switch
        {
            ProductType.Floor => new RoomFloorItem
            {
                ObjectId = RoomObjectId.From(entity.Id),
                OwnerId = entity.PlayerEntityId,
                OwnerName = string.Empty,
                Definition = definition,
                PendingStuffDataRaw = entity.StuffData ?? string.Empty,
            },

            ProductType.Wall => new RoomWallItem
            {
                ObjectId = RoomObjectId.From(entity.Id),
                OwnerId = entity.PlayerEntityId,
                OwnerName = string.Empty,
                Definition = definition,
                PendingStuffDataRaw = entity.StuffData ?? string.Empty,
            },

            _ => throw new TurboException(TurboErrorCodeEnum.InvalidFurnitureProductType),
        };
    }
}
