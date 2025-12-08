using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Logging;
using Turbo.Players.Grains;
using Turbo.Primitives;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Factories;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Factories;

internal sealed class RoomItemsLoader(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    IFurnitureDefinitionProvider defsProvider
) : IRoomItemsLoader
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;

    public async Task<(
        IReadOnlyList<IRoomFloorItem>,
        IReadOnlyList<IRoomWallItem>,
        IReadOnlyDictionary<long, string>
    )> LoadByRoomIdAsync(long roomId, CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .Furnitures.AsNoTracking()
                .Where(x => x.RoomEntityId == roomId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var floorItems = new List<IRoomFloorItem>();
            var wallItems = new List<IRoomWallItem>();

            var ownerIdsUnique = entities.Select(x => (long)x.PlayerEntityId).Distinct().ToList();
            var ownerNames = await _grainFactory
                .GetGrain<IPlayerDirectoryGrain>(PlayerDirectoryGrain.SINGLETON_KEY)
                .GetPlayerNamesAsync(ownerIdsUnique, ct)
                .ConfigureAwait(false);

            foreach (var entity in entities)
            {
                try
                {
                    var item = CreateFromEntity(entity);

                    item.SetOwnerName(
                        ownerNames.TryGetValue(entity.PlayerEntityId, out var name)
                            ? name ?? string.Empty
                            : string.Empty
                    );

                    if (item is IRoomFloorItem floorItem)
                    {
                        floorItem.SetPosition(entity.X, entity.Y, entity.Z);
                        floorItem.SetRotation(entity.Rotation);

                        floorItems.Add(floorItem);
                    }
                    else if (item is IRoomWallItem wallItem)
                    {
                        wallItem.SetPosition(entity.X, entity.Y, entity.Z);
                        wallItem.SetWallOffset(entity.WallOffset);
                        wallItem.SetRotation(entity.Rotation);

                        wallItems.Add(wallItem);
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return (floorItems, wallItems, ownerNames);
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

    public IRoomItem CreateFromFurnitureItemSnapshot(FurnitureItemSnapshot snapshot)
    {
        var definition = snapshot.Definition;

        if (definition.ProductType == ProductType.Floor)
        {
            return new RoomFloorItem
            {
                ObjectId = RoomObjectId.From(snapshot.ItemId),
                OwnerId = snapshot.OwnerId,
                OwnerName = string.Empty,
                Definition = definition,
                PendingStuffDataRaw = snapshot.StuffDataJson,
            };
        }

        if (definition.ProductType == ProductType.Wall)
        {
            return new RoomWallItem
            {
                ObjectId = RoomObjectId.From(snapshot.ItemId),
                OwnerId = snapshot.OwnerId,
                OwnerName = string.Empty,
                Definition = definition,
                PendingStuffDataRaw = snapshot.StuffDataJson,
            };
        }

        throw new TurboException(TurboErrorCodeEnum.InvalidFurnitureProductType);
    }
}
