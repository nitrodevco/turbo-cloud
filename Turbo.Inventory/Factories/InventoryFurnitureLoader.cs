using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Furniture;
using Turbo.Inventory.Furniture;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Inventory.Factories;

internal sealed class InventoryFurnitureLoader(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IFurnitureDefinitionProvider defsProvider,
    IStuffDataFactory stuffDataFactory,
    ILogger<IInventoryFurnitureLoader> logger
) : IInventoryFurnitureLoader
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;
    private readonly IStuffDataFactory _stuffDataFactory = stuffDataFactory;
    private readonly ILogger<IInventoryFurnitureLoader> _logger = logger;

    public async Task<IReadOnlyList<IFurnitureItem>> LoadByPlayerIdAsync(
        PlayerId playerId,
        string ownerName,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .Furnitures.AsNoTracking()
            .Where(x => x.PlayerEntityId == (int)playerId && x.RoomEntityId == null)
            .Select(x => new
            {
                x.Id,
                x.FurnitureDefinitionEntityId,
                x.ExtraData,
                x.CreatedAt,
            })
            .ToListAsync(ct);

        var items = new List<IFurnitureItem>(rows.Count);

        foreach (var row in rows)
        {
            var definition = _defsProvider.TryGetDefinition(row.FurnitureDefinitionEntityId);

            // A row whose definition is gone cannot be shown or placed; skip it, loudly.
            if (definition is null)
            {
                _logger.LogWarning(
                    "Furniture {ItemId} of player {PlayerId} uses missing definition {DefinitionId}; left out of the inventory",
                    row.Id,
                    playerId,
                    row.FurnitureDefinitionEntityId
                );

                continue;
            }

            items.Add(
                Create(row.Id, playerId, ownerName, definition, row.ExtraData, row.CreatedAt)
            );
        }

        return items;
    }

    public IFurnitureItem Create(
        RoomObjectId itemId,
        PlayerId ownerId,
        string ownerName,
        FurnitureDefinitionSnapshot definition,
        string? extraDataJson,
        DateTime? createdAtUtc
    ) =>
        Build(
            itemId,
            ownerId,
            ownerName,
            definition,
            extraDataJson,
            // TODO the stuff data type belongs to the furniture logic, which inventories do not run.
            StuffDataType.LegacyKey,
            createdAtUtc
        );

    public IFurnitureItem CreateFromFurnitureItemSnapshot(
        FurnitureItemSnapshot snapshot,
        PlayerId ownerId,
        string ownerName
    ) =>
        Build(
            snapshot.ItemId,
            ownerId,
            ownerName,
            snapshot.Definition,
            snapshot.ExtraData,
            (StuffDataType)snapshot.StuffData.StuffBitmask,
            snapshot.CreatedAtUtc
        );

    public IFurnitureItem CreateFromRoomItemSnapshot(RoomItemSnapshot snapshot)
    {
        var definition =
            _defsProvider.TryGetDefinition(snapshot.DefinitionId)
            ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);

        return Build(
            snapshot.ObjectId,
            snapshot.OwnerId,
            snapshot.OwnerName,
            definition,
            snapshot.ExtraData,
            (StuffDataType)snapshot.StuffData.StuffBitmask,
            null
        );
    }

    /// <summary>
    /// The one place an inventory item is assembled: the stuff data always comes from the
    /// stuff section of the item's own extra data.
    /// </summary>
    private FurnitureItem Build(
        RoomObjectId itemId,
        PlayerId ownerId,
        string ownerName,
        FurnitureDefinitionSnapshot definition,
        string? extraDataJson,
        StuffDataType stuffDataType,
        DateTime? createdAtUtc
    )
    {
        var extraData = new ExtraData(extraDataJson);

        return new FurnitureItem
        {
            ItemId = itemId,
            OwnerId = ownerId,
            OwnerName = ownerName,
            Definition = definition,
            ExtraData = extraData,
            StuffData = _stuffDataFactory.CreateStuffDataFromExtraData(stuffDataType, extraData),
            CreatedAtUtc = createdAtUtc,
        };
    }
}
