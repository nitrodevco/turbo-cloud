using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Furniture.Wall;

namespace Turbo.Rooms.Providers;

internal sealed class RoomItemsProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IRoomItemsProvider> logger,
    IGrainFactory grainFactory,
    IFurnitureDefinitionProvider defsProvider
) : IRoomItemsProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<IRoomItemsProvider> _logger = logger;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;

    public async Task<(
        IReadOnlyList<IRoomFloorItem>,
        IReadOnlyList<IRoomWallItem>,
        IReadOnlyDictionary<PlayerId, string>
    )> LoadByRoomIdAsync(RoomId roomId, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var entities = await dbCtx
            .Furnitures.AsNoTracking()
            .Where(x => x.RoomEntityId == (int)roomId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Borrowed furni has its own table and its own id band, but in the room it is furni like
        // any other, so it is loaded here and merged into the same two lists.
        var borrowed = await dbCtx
            .BuildersClubFurnitures.AsNoTracking()
            .Where(x => x.RoomEntityId == (int)roomId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var floorItems = new List<IRoomFloorItem>();
        var wallItems = new List<IRoomWallItem>();

        // A borrower is not an owner, but their name is what the room caches against the item,
        // so both sets of ids are resolved together.
        var ownerIdsUnique = entities
            .Select(x => (PlayerId)x.PlayerEntityId)
            .Concat(borrowed.Select(x => (PlayerId)x.PlacedByPlayerEntityId))
            .Distinct()
            .ToList();
        var ownerNames = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync(ownerIdsUnique, ct)
            .ConfigureAwait(false);

        foreach (var entity in entities)
        {
            Place(
                CreateOrNull(entity),
                entity.ExtraData,
                entity.PlayerEntityId,
                entity.X,
                entity.Y,
                entity.Z,
                entity.Rotation,
                entity.WallOffset
            );
        }

        foreach (var entity in borrowed)
        {
            Place(
                CreateOrNull(entity),
                entity.ExtraData,
                entity.PlacedByPlayerEntityId,
                entity.X,
                entity.Y,
                entity.Z,
                entity.Rotation,
                entity.WallOffset
            );
        }

        return (floorItems, wallItems, ownerNames);

        void Place(
            IRoomItem? item,
            string? extraData,
            int ownerId,
            int x,
            int y,
            double z,
            Rotation rotation,
            int wallOffset
        )
        {
            if (item is null)
                return;

            item.SetExtraData(extraData);
            item.SetOwnerName(
                ownerNames.TryGetValue(ownerId, out var name) ? name ?? string.Empty : string.Empty
            );
            item.SetPosition(x, y);
            item.SetPositionZ(z);
            item.SetRotation(rotation);

            if (item is IRoomFloorItem floorItem)
            {
                floorItems.Add(floorItem);

                return;
            }

            if (item is IRoomWallItem wallItem)
            {
                wallItem.SetWallOffset(wallOffset);

                wallItems.Add(wallItem);
            }
        }
    }

    /// <summary>
    /// A row whose definition the hotel no longer has cannot be made into furni. It is skipped
    /// and logged with its id rather than taking the whole room's furni down with it.
    /// </summary>
    private IRoomItem? CreateOrNull(FurnitureEntity entity)
    {
        try
        {
            return CreateFromEntity(entity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Skipped furniture {ItemId} of room {RoomId}",
                entity.Id,
                entity.RoomEntityId
            );

            return null;
        }
    }

    /// <summary>The same, for a furni the Builders Club lent.</summary>
    private IRoomItem? CreateOrNull(BuildersClubFurnitureEntity entity)
    {
        try
        {
            return CreateFromBuildersClubEntity(entity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Skipped borrowed furniture {ItemId} of room {RoomId}",
                entity.RoomObjectId,
                entity.RoomEntityId
            );

            return null;
        }
    }

    public IRoomItem CreateFromEntity(FurnitureEntity entity) =>
        Create(
            _defsProvider.TryGetDefinition(entity.FurnitureDefinitionEntityId)
                ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound),
            entity.Id,
            entity.PlayerEntityId
        );

    public IRoomItem CreateFromBuildersClubEntity(BuildersClubFurnitureEntity entity) =>
        Create(
            _defsProvider.TryGetDefinition(entity.FurnitureDefinitionEntityId)
                ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound),
            entity.RoomObjectId,
            entity.PlacedByPlayerEntityId
        );

    public IRoomFloorItem CreateFloorItem(
        RoomObjectId objectId,
        PlayerId ownerId,
        FurnitureDefinitionSnapshot definition
    )
    {
        if (definition.ProductType != ProductType.Floor)
            throw new TurboException(TurboErrorCodeEnum.InvalidFurnitureProductType);

        return (IRoomFloorItem)CreateFromDefinition(objectId, ownerId, definition);
    }

    public IRoomItem CreateFromDefinition(
        RoomObjectId objectId,
        PlayerId ownerId,
        FurnitureDefinitionSnapshot definition
    )
    {
        var item = Create(definition, objectId, ownerId);

        item.SetExtraData(null);

        return item;
    }

    public IRoomItem CreateFromFurnitureItemSnapshot(FurnitureItemSnapshot snapshot)
    {
        var item = Create(snapshot.Definition, snapshot.ItemId, snapshot.OwnerId);

        item.SetOwnerName(snapshot.OwnerName);
        item.SetExtraData(snapshot.ExtraData);

        return item;
    }

    private static IRoomItem Create(
        FurnitureDefinitionSnapshot definition,
        RoomObjectId objectId,
        PlayerId ownerId
    ) =>
        definition.ProductType switch
        {
            ProductType.Floor => new RoomFloorItem
            {
                ObjectId = objectId,
                OwnerId = ownerId,
                OwnerName = string.Empty,
                Definition = definition,
            },

            ProductType.Wall => new RoomWallItem
            {
                ObjectId = objectId,
                OwnerId = ownerId,
                OwnerName = string.Empty,
                Definition = definition,
            },

            _ => throw new TurboException(TurboErrorCodeEnum.InvalidFurnitureProductType),
        };
}
