using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Extensions;
using Turbo.Database.Movements;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Furniture.Custody;

/// <inheritdoc cref="IFurnitureCustody"/>
public sealed class FurnitureCustody(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAssetMovementRecorder movements,
    ILogger<FurnitureCustody> logger
) : IFurnitureCustody
{
    public Task<bool> DestroyInInventoryAsync(
        PlayerId owner,
        FurnitureItemSnapshot item,
        MovementCause cause,
        CancellationToken ct
    ) =>
        dbCtxFactory.InTransactionAsync(
            async dbCtx =>
            {
                var deleted = await InInventory(dbCtx, owner, [item.ItemId.Value])
                    .ExecuteDeleteAsync(ct);

                if (deleted == 0)
                    return UnitOfWork.Rollback(false);

                movements.Record(
                    dbCtx,
                    Movement(
                        cause,
                        0,
                        Furni(item.ItemId.Value, item.Definition.Id),
                        Inventory(owner),
                        AssetPlace.Nowhere,
                        new AssetSnapshot(item.Definition.Id, item.ExtraData)
                    )
                );

                return UnitOfWork.Commit(true);
            },
            ct
        );

    public async Task<IReadOnlyList<CustodyFurni>> CreateInInventoryAsync(
        PlayerId owner,
        IReadOnlyList<FurniToMake> furni,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (furni.Count == 0)
            return [];

        var rows = furni.Select(x => NewRow(owner, x)).ToList();

        await dbCtxFactory.InTransactionAsync(
            async dbCtx =>
            {
                dbCtx.AddRange(rows);

                // The ids the movements name exist once the rows are inserted.
                await dbCtx.SaveChangesAsync(ct);

                movements.Record(dbCtx, Created(cause, rows, _ => Inventory(owner)));

                return UnitOfWork.Commit(true);
            },
            ct
        );

        return [.. rows.Select(ToCustody)];
    }

    public async Task<IReadOnlyList<CustodyFurni>> CreateTeleportPairsInInventoryAsync(
        PlayerId owner,
        IReadOnlyList<int> definitionIds,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (definitionIds.Count == 0)
            return [];

        var rows = definitionIds
            .SelectMany(id => new[] { id, id })
            .Select(id => NewRow(owner, new FurniToMake(id, null)))
            .ToList();

        await dbCtxFactory.InTransactionAsync(
            async dbCtx =>
            {
                dbCtx.AddRange(rows);

                // The pair's ids exist only once the rows are inserted; the links are saved with the commit.
                await dbCtx.SaveChangesAsync(ct);

                for (var i = 0; i < rows.Count; i += 2)
                {
                    rows[i].ExtraData = TeleportFurniture.PairExtraData(rows[i + 1].Id);
                    rows[i + 1].ExtraData = TeleportFurniture.PairExtraData(rows[i].Id);
                }

                movements.Record(dbCtx, Created(cause, rows, _ => Inventory(owner)));

                return UnitOfWork.Commit(true);
            },
            ct
        );

        return [.. rows.Select(ToCustody)];
    }

    public async Task<CustodyFurni> CreatePresentInInventoryAsync(
        PlayerId owner,
        FurniToMake present,
        FurniToMake content,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var presentRow = NewRow(owner, present);
        var contentRow = NewRow(owner, content);

        await dbCtxFactory.InTransactionAsync(
            async dbCtx =>
            {
                dbCtx.Add(presentRow);

                // The present's id exists only once its row is inserted.
                await dbCtx.SaveChangesAsync(ct);

                contentRow.ChestItemEntityId = presentRow.Id;
                dbCtx.Add(contentRow);

                await dbCtx.SaveChangesAsync(ct);

                movements.Record(
                    dbCtx,
                    Created(
                        cause,
                        [presentRow, contentRow],
                        row => row == presentRow ? Inventory(owner) : Holder(owner, presentRow.Id)
                    )
                );

                return UnitOfWork.Commit(true);
            },
            ct
        );

        return ToCustody(presentRow);
    }

    public Task<CustodyFurni?> TakeOutOfPresentAsync(
        PlayerId opener,
        RoomObjectId presentId,
        MovementCause cause,
        CancellationToken ct
    ) =>
        dbCtxFactory.InTransactionAsync(
            async dbCtx =>
            {
                var row = await dbCtx
                    .Furnitures.Where(x => x.ChestItemEntityId == presentId.Value)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(ct);

                if (row is null)
                    return UnitOfWork.Rollback<CustodyFurni?>(null);

                var wrappedFor = (PlayerId)row.PlayerEntityId;

                row.ChestItemEntityId = null;
                row.PlayerEntityId = opener.Value;

                movements.Record(
                    dbCtx,
                    Movement(
                        cause,
                        0,
                        Furni(row.Id, row.FurnitureDefinitionEntityId),
                        Holder(wrappedFor, presentId.Value),
                        Inventory(opener)
                    )
                );

                return UnitOfWork.Commit<CustodyFurni?>(ToCustody(row));
            },
            ct
        );

    public Task<bool> TransferBetweenInventoriesAsync(
        PlayerId from,
        PlayerId to,
        IReadOnlyList<FurnitureItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var ids = items.Select(x => x.ItemId.Value).ToList();

        return dbCtxFactory.InTransactionAsync(
            async dbCtx =>
            {
                var updated = await InInventory(dbCtx, from, ids)
                    .ExecuteUpdateAsync(up => up.SetProperty(x => x.PlayerEntityId, to.Value), ct);

                if (updated != ids.Count)
                {
                    logger.LogError(
                        "Transfer of {Count} items from player {PlayerId} to {ToPlayerId} changed {Updated} rows; rolled back",
                        ids.Count,
                        from,
                        to,
                        updated
                    );

                    return UnitOfWork.Rollback(false);
                }

                var at = DateTime.UtcNow;

                movements.Record(
                    dbCtx,
                    [
                        .. items.Select(
                            (item, i) =>
                                Movement(
                                    cause,
                                    i,
                                    Furni(item.ItemId.Value, item.Definition.Id),
                                    Inventory(from),
                                    Inventory(to),
                                    at: at
                                )
                        ),
                    ]
                );

                return UnitOfWork.Commit(true);
            },
            ct
        );
    }

    /// <summary>The rows of these ids an inventory holds: its owner's, in no room and no holder.</summary>
    private static IQueryable<FurnitureEntity> InInventory(
        TurboDbContext dbCtx,
        PlayerId owner,
        IReadOnlyCollection<int> ids
    ) =>
        dbCtx.Furnitures.Where(x =>
            ids.Contains(x.Id)
            && x.PlayerEntityId == owner.Value
            && x.RoomEntityId == null
            && x.ChestItemEntityId == null
        );

    private static FurnitureEntity NewRow(PlayerId owner, FurniToMake furni) =>
        new()
        {
            PlayerEntityId = owner.Value,
            FurnitureDefinitionEntityId = furni.DefinitionId,
            ExtraData = furni.ExtraDataJson,
        };

    private static CustodyFurni ToCustody(FurnitureEntity row) =>
        new(row.Id, row.FurnitureDefinitionEntityId, row.ExtraData, row.CreatedAt);

    /// <summary>Rows just made, one movement each from nowhere, carrying what they were made from.</summary>
    private static List<AssetMovement> Created(
        MovementCause cause,
        IReadOnlyList<FurnitureEntity> rows,
        Func<FurnitureEntity, AssetPlace> placeOf
    )
    {
        var at = DateTime.UtcNow;

        return
        [
            .. rows.Select(
                (row, i) =>
                    Movement(
                        cause,
                        i,
                        Furni(row.Id, row.FurnitureDefinitionEntityId),
                        AssetPlace.Nowhere,
                        placeOf(row),
                        new AssetSnapshot(row.FurnitureDefinitionEntityId, row.ExtraData),
                        at
                    )
            ),
        ];
    }

    private static AssetMovement Movement(
        MovementCause cause,
        int index,
        AssetRef asset,
        AssetPlace from,
        AssetPlace to,
        AssetSnapshot? snapshot = null,
        DateTime? at = null
    ) =>
        AssetMovement.For(
            cause,
            checked((short)(cause.FirstSeq + index)),
            at ?? DateTime.UtcNow,
            asset,
            from,
            to,
            snapshot: snapshot
        );

    private static AssetRef Furni(int id, int definitionId) =>
        new(AssetKind.Furni, id, definitionId, null);

    private static AssetPlace Inventory(PlayerId owner) =>
        new(owner.Value, PlaceKind.Inventory, 0, null);

    private static AssetPlace Holder(PlayerId owner, int holderId) =>
        new(owner.Value, PlaceKind.Holder, holderId, null);
}
