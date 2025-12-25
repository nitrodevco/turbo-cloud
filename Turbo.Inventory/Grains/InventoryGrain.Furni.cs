using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Database.Entities.Furniture;
using Turbo.Furniture;
using Turbo.Inventory.Furniture;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Inventory.Grains;

public sealed partial class InventoryGrain
{
    public Task EnsureFurnitureReadyAsync(CancellationToken ct) =>
        _furniModule.EnsureFurnitureReadyAsync(ct);

    public async Task<bool> AddFurnitureAsync(IFurnitureItem item, CancellationToken ct)
    {
        if (!await _furniModule.AddFurnitureAsync(item, ct))
            return false;

        var presence = _grainFactory.GetPlayerPresenceGrain(this.GetPrimaryKeyLong());

        await presence.OnFurnitureAddedAsync(item.GetSnapshot(), ct);

        return true;
    }

    public async Task<bool> AddFurnitureFromRoomItemSnapshotAsync(
        RoomItemSnapshot snapshot,
        CancellationToken ct
    )
    {
        var item = _furnitureItemsLoader.CreateFromRoomItemSnapshot(snapshot);

        if (!await _furniModule.AddFurnitureAsync(item, ct))
            return false;

        var presence = _grainFactory.GetPlayerPresenceGrain(this.GetPrimaryKeyLong());

        await presence.OnFurnitureAddedAsync(item.GetSnapshot(), ct);

        return true;
    }

    public async Task<bool> RemoveFurnitureAsync(RoomObjectId itemId, CancellationToken ct)
    {
        if (!await _furniModule.RemoveFurnitureAsync(itemId, ct))
            return false;

        var presence = _grainFactory.GetPlayerPresenceGrain(this.GetPrimaryKeyLong());

        await presence.OnFurnitureRemovedAsync(itemId, ct);

        return true;
    }

    public async Task GrantCatalogOfferAsync(
        CatalogOfferSnapshot offer,
        string extraParam,
        int quantity,
        CancellationToken ct
    )
    {
        quantity = Math.Max(1, quantity);

        var entities = new List<FurnitureEntity>();

        foreach (var product in offer.Products)
        {
            if (product.ProductType is ProductType.Floor || product.ProductType is ProductType.Wall)
            {
                var def =
                    _furnitureDefinitionProvider.TryGetDefinition(product.FurniDefinitionId)
                    ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);

                for (int i = 0; i < quantity; i++)
                    entities.Add(
                        new FurnitureEntity
                        {
                            PlayerEntityId = (int)this.GetPrimaryKeyLong(),
                            FurnitureDefinitionEntityId = def.Id,
                        }
                    );

                continue;
            }
        }

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            dbCtx.AddRange(entities);

            await dbCtx.SaveChangesAsync(ct);

            foreach (var entity in entities)
            {
                var def =
                    _furnitureDefinitionProvider.TryGetDefinition(
                        entity.FurnitureDefinitionEntityId
                    ) ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);

                // TODO need to batch these

                await AddFurnitureAsync(
                    new FurnitureItem()
                    {
                        ItemId = entity.Id,
                        OwnerId = entity.PlayerEntityId,
                        OwnerName = string.Empty,
                        Definition = def,
                        ExtraData = new ExtraData("{}"),
                        StuffData = _stuffDataFactory.CreateStuffData((int)StuffDataType.LegacyKey),
                    },
                    ct
                );
            }
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    public Task<FurnitureItemSnapshot?> GetItemSnapshotAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) => _furniModule.GetItemSnapshotAsync(itemId, ct);

    public Task<ImmutableArray<FurnitureItemSnapshot>> GetAllItemSnapshotsAsync(
        CancellationToken ct
    ) => _furniModule.GetAllItemSnapshotsAsync(ct);
}
