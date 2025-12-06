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
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Furniture;

namespace Turbo.Inventory.Furniture;

internal sealed class FurnitureItemsLoader(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IFurnitureDefinitionProvider defsProvider,
    IStuffDataFactory stuffDataFactory
) : IFurnitureItemsLoader
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;
    private readonly IStuffDataFactory _stuffDataFactory = stuffDataFactory;

    public async Task<IReadOnlyList<IFurnitureItem>> LoadByPlayerIdAsync(
        long playerId,
        CancellationToken ct
    )
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .Furnitures.AsNoTracking()
                .Where(x => x.PlayerEntityId == playerId && x.RoomEntityId == null)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var items = new List<IFurnitureItem>();

            foreach (var entity in entities)
            {
                try
                {
                    var item = CreateFromEntity(entity);

                    items.Add(item);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return items;
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

        return new FurnitureItem()
        {
            ItemId = entity.Id,
            OwnerId = entity.PlayerEntityId,
            Definition = definition,
            StuffData = _stuffDataFactory.CreateStuffDataFromJson(
                (int)StuffDataType.LegacyKey,
                entity.StuffData ?? string.Empty
            ),
        };
    }
}
