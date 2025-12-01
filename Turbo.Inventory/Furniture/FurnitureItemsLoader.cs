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
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Furniture;

namespace Turbo.Inventory.Furniture;

internal sealed class FurnitureItemsLoader(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IFurnitureDefinitionProvider defsProvider,
    IStuffDataFactory stuffDataFactory
) : IFurnitureItemsLoader
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;
    private readonly IStuffDataFactory _stuffDataFactory = stuffDataFactory;

    public async Task<(
        IReadOnlyList<IFurnitureFloorItem>,
        IReadOnlyList<IFurnitureWallItem>
    )> LoadByPlayerIdAsync(long playerId, CancellationToken ct)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .Furnitures.AsNoTracking()
                .Where(x => x.PlayerEntityId == playerId && x.RoomEntityId == null)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var floorItems = new List<IFurnitureFloorItem>();
            var wallItems = new List<IFurnitureWallItem>();

            foreach (var entity in entities)
            {
                try
                {
                    var item = CreateFromEntity(entity);

                    switch (item)
                    {
                        case IFurnitureFloorItem floorItem:
                            floorItems.Add(floorItem);
                            continue;
                        case IFurnitureWallItem wallItem:
                            wallItems.Add(wallItem);
                            continue;
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

    public IFurnitureItem CreateFromEntity(FurnitureEntity entity)
    {
        var definition =
            _defsProvider.TryGetDefinition(entity.FurnitureDefinitionEntityId)
            ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);

        return definition.ProductType switch
        {
            ProductType.Floor => new FurnitureFloorItem
            {
                ItemId = entity.Id,
                Definition = definition,
                StuffData = _stuffDataFactory.CreateStuffDataFromJson(
                    (int)StuffDataType.LegacyKey,
                    entity.StuffData ?? string.Empty
                ),
            },

            ProductType.Wall => new FurnitureWallItem
            {
                ItemId = entity.Id,
                Definition = definition,
                StuffData = entity.StuffData ?? string.Empty,
            },

            _ => throw new TurboException(TurboErrorCodeEnum.InvalidFurnitureProductType),
        };
    }
}
