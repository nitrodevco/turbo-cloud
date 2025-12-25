using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Inventory.Furniture;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Inventory.Factories;

internal sealed class InventoryFurnitureLoader(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IFurnitureDefinitionProvider defsProvider,
    IStuffDataFactory stuffDataFactory
) : IInventoryFurnitureLoader
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;
    private readonly IStuffDataFactory _stuffDataFactory = stuffDataFactory;

    public async Task<IReadOnlyList<IFurnitureItem>> LoadByPlayerIdAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .Furnitures.AsNoTracking()
                .Where(x => x.PlayerEntityId == (int)playerId && x.RoomEntityId == null)
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

        // TODO we need to get the correct stuff data key

        return new FurnitureItem()
        {
            ItemId = entity.Id,
            OwnerId = entity.PlayerEntityId,
            Definition = definition,
            StuffData = _stuffDataFactory.CreateStuffDataFromJson(
                (int)StuffDataType.LegacyKey,
                entity.ExtraData ?? string.Empty
            ),
        };
    }

    public IFurnitureItem CreateFromRoomItemSnapshot(RoomItemSnapshot snapshot)
    {
        var definition =
            _defsProvider.TryGetDefinition(snapshot.DefinitionId)
            ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);

        return new FurnitureItem()
        {
            ItemId = snapshot.ObjectId,
            OwnerId = snapshot.OwnerId,
            Definition = definition,
            StuffData = _stuffDataFactory.CreateStuffDataFromJson(
                snapshot.StuffData.StuffBitmask,
                snapshot.ExtraDataJson
            ),
        };
    }
}
