using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Furniture;
using Turbo.Inventory.Furniture;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Inventory.Factories;

internal sealed class InventoryFurnitureLoader(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IFurnitureDefinitionProvider defsProvider,
    IStuffDataFactory stuffDataFactory,
    IGrainFactory grainFactory
) : IInventoryFurnitureLoader
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;
    private readonly IStuffDataFactory _stuffDataFactory = stuffDataFactory;
    private readonly IGrainFactory _grainFactory = grainFactory;

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

            var ownerName = await _grainFactory
                .GetPlayerDirectoryGrain()
                .GetPlayerNameAsync(playerId, ct)
                .ConfigureAwait(false);

            foreach (var entity in entities)
            {
                try
                {
                    var item = CreateFromEntity(entity, ownerName);

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

    public IFurnitureItem CreateFromEntity(FurnitureEntity entity, string? ownerName)
    {
        var definition =
            _defsProvider.TryGetDefinition(entity.FurnitureDefinitionEntityId)
            ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);

        // TODO we need to get the correct stuff data key

        var extraData = new ExtraData(entity.ExtraData);
        var jsonData = extraData.TryGetSection("stuff", out var element)
            ? element.GetRawText()
            : "{}";

        return new FurnitureItem()
        {
            ItemId = entity.Id,
            OwnerId = entity.PlayerEntityId,
            OwnerName = ownerName ?? string.Empty,
            Definition = definition,
            ExtraData = extraData,
            StuffData = _stuffDataFactory.CreateStuffDataFromJson(
                StuffDataType.LegacyKey,
                jsonData
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
            OwnerName = snapshot.OwnerName,
            Definition = definition,
            ExtraData = new ExtraData(snapshot.ExtraData),
            StuffData = _stuffDataFactory.CreateStuffDataFromJson(
                (StuffDataType)snapshot.StuffData.StuffBitmask,
                snapshot.ExtraData
            ),
        };
    }
}
