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
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Furniture.Custody;

/// <inheritdoc cref="IFurnitureCustody"/>
public sealed partial class FurnitureCustody(
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
    ) => DestroyInInventoryAsync(owner, [item], cause, ct);

    public Task<bool> DestroyInInventoryAsync(
        PlayerId owner,
        IReadOnlyList<FurnitureItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    ) =>
        dbCtxFactory.InTransactionAsync(
            async dbCtx =>
                await DestroyInInventoryAsync(dbCtx, owner, items, cause, ct).ConfigureAwait(false)
                    ? UnitOfWork.Commit(true)
                    : UnitOfWork.Rollback(false),
            ct
        );

    public async Task<bool> DestroyInInventoryAsync(
        TurboDbContext dbCtx,
        PlayerId owner,
        IReadOnlyList<FurnitureItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var deleted = await InInventory(dbCtx, owner, IdsOf(items))
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (deleted != items.Count)
            return false;

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
                            Inventory(owner),
                            AssetPlace.Nowhere,
                            new AssetSnapshot(item.Definition.Id, item.ExtraData),
                            at
                        )
                ),
            ]
        );

        return true;
    }

    public async Task<bool> SetExtraDataInInventoryAsync(
        PlayerId owner,
        RoomObjectId itemId,
        string extraDataJson,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        await using (dbCtx.ConfigureAwait(false))
        {
            return await InInventory(dbCtx, owner, [itemId.Value])
                    .ExecuteUpdateAsync(up => up.SetProperty(x => x.ExtraData, extraDataJson), ct)
                    .ConfigureAwait(false) == 1;
        }
    }

    public async Task<bool> PutInHolderAsync(
        TurboDbContext dbCtx,
        PlayerId from,
        IReadOnlyList<FurnitureItemSnapshot> items,
        FurniHolder holder,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var moved = await InInventory(dbCtx, from, IdsOf(items))
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(x => x.ChestItemEntityId, holder.ItemId)
                        .SetProperty(x => x.PlayerEntityId, holder.HeldFor.Value)
                        .SetProperty(x => x.ChestTransactionId, holder.TransactionId),
                ct
            )
            .ConfigureAwait(false);

        if (moved != items.Count)
            return false;

        movements.Record(dbCtx, Moved(cause, items, Inventory(from), Holder(holder)));

        return true;
    }

    public async Task<bool> TakeOutOfHolderAsync(
        TurboDbContext dbCtx,
        FurniHolder holder,
        IReadOnlyList<FurnitureItemSnapshot> items,
        PlayerId to,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var ids = IdsOf(items);
        var moved = await dbCtx
            .Furnitures.Where(x => ids.Contains(x.Id) && x.ChestItemEntityId == holder.ItemId)
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(x => x.ChestItemEntityId, (int?)null)
                        .SetProperty(x => x.ChestTransactionId, (long?)null)
                        .SetProperty(x => x.HeldPosition, (int?)null)
                        .SetProperty(x => x.PlayerEntityId, to.Value),
                ct
            )
            .ConfigureAwait(false);

        if (moved != items.Count)
            return false;

        movements.Record(dbCtx, Moved(cause, items, Holder(holder), Inventory(to)));

        return true;
    }

    public Task<IReadOnlyList<CustodyFurni>> CreateInRoomAsync(
        RoomId roomId,
        PlayerId owner,
        IReadOnlyList<FurniToPlace> furni,
        MovementCause cause,
        CancellationToken ct
    ) =>
        dbCtxFactory.InTransactionAsync(
            async dbCtx =>
                UnitOfWork.Commit(
                    await CreateInRoomAsync(dbCtx, roomId, owner, furni, cause, ct)
                        .ConfigureAwait(false)
                ),
            ct
        );

    public async Task<IReadOnlyList<CustodyFurni>> CreateInRoomAsync(
        TurboDbContext dbCtx,
        RoomId roomId,
        PlayerId owner,
        IReadOnlyList<FurniToPlace> furni,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (furni.Count == 0)
            return [];

        var rows = furni
            .Select(x =>
            {
                var row = NewRow(owner, x.Furni);

                row.RoomEntityId = roomId.Value;
                row.X = x.Position.X;
                row.Y = x.Position.Y;
                row.Z = x.Position.Z;
                row.Rotation = x.Position.Rotation;
                row.WallOffset = x.Position.WallOffset ?? 0;

                return row;
            })
            .ToList();

        dbCtx.AddRange(rows);

        // The ids the movements name exist once the rows are inserted.
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        var positions = rows.Select((row, i) => (row, furni[i].Position)).ToDictionary();

        movements.Record(dbCtx, Created(cause, rows, row => InRoom(owner, roomId, positions[row])));

        return [.. rows.Select(ToCustody)];
    }

    public async Task<int> SendHomeLeftInRoomAsync(
        TurboDbContext dbCtx,
        RoomId roomId,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var inRoom = dbCtx.Furnitures.Where(x => x.RoomEntityId == roomId.Value);

        // Read only while recording: otherwise this stays the one statement it always was.
        var left = movements.IsRecording
            ? await inRoom
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.FurnitureDefinitionEntityId,
                    x.PlayerEntityId,
                    Position = new RoomPosition(x.X, x.Y, x.Z, x.Rotation, x.WallOffset),
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        var sent = await inRoom
            .ExecuteUpdateAsync(up => up.SetProperty(x => x.RoomEntityId, (int?)null), ct)
            .ConfigureAwait(false);

        var at = DateTime.UtcNow;

        movements.Record(
            dbCtx,
            [
                .. left.Select(
                    (row, i) =>
                        Movement(
                            cause,
                            i,
                            Furni(row.Id, row.FurnitureDefinitionEntityId),
                            InRoom((PlayerId)row.PlayerEntityId, roomId, row.Position),
                            Inventory((PlayerId)row.PlayerEntityId),
                            at: at
                        )
                ),
            ]
        );

        return sent;
    }

    public Task BorrowIntoRoomAsync(
        RoomId roomId,
        RoomItemSnapshot item,
        MovementCause cause,
        CancellationToken ct
    ) =>
        dbCtxFactory.InTransactionAsync(
            dbCtx =>
            {
                dbCtx.BuildersClubFurnitures.Add(
                    new BuildersClubFurnitureEntity
                    {
                        RoomEntityId = roomId.Value,
                        RoomObjectId = item.ObjectId.Value,
                        FurnitureDefinitionEntityId = item.DefinitionId,
                        PlacedByPlayerEntityId = item.OwnerId.Value,
                        X = item.X,
                        Y = item.Y,
                        Z = item.Z,
                        Rotation = item.Rotation,
                        WallOffset = WallOffsetOf(item) ?? 0,
                        ExtraData = item.ExtraData,
                    }
                );

                movements.Record(
                    dbCtx,
                    Movement(
                        cause,
                        0,
                        Borrowed(item),
                        AssetPlace.Nowhere,
                        InRoom(NOBODY, roomId, item)
                    )
                );

                return Task.FromResult(UnitOfWork.Commit(true));
            },
            ct
        );

    public async Task<int> GiveBackBorrowedAsync(
        TurboDbContext dbCtx,
        RoomId roomId,
        IReadOnlyList<RoomItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (items.Count == 0)
            return 0;

        var ids = items.Select(x => x.ObjectId.Value).ToList();
        var rows = dbCtx.BuildersClubFurnitures.Where(x =>
            x.RoomEntityId == roomId.Value && ids.Contains(x.RoomObjectId)
        );

        // Read only while recording: otherwise this stays the one statement it always was.
        var present = movements.IsRecording
            ? (
                await rows.Select(x => x.RoomObjectId).ToListAsync(ct).ConfigureAwait(false)
            ).ToHashSet()
            : [];

        var given = await rows.ExecuteDeleteAsync(ct).ConfigureAwait(false);

        var at = DateTime.UtcNow;
        var moved = new List<AssetMovement>(present.Count);

        foreach (var item in items)
        {
            if (present.Contains(item.ObjectId.Value))
                moved.Add(
                    Movement(
                        cause,
                        moved.Count,
                        Borrowed(item),
                        InRoom(NOBODY, roomId, item),
                        AssetPlace.Nowhere,
                        at: at
                    )
                );
        }

        movements.Record(dbCtx, moved);

        return given;
    }

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

        await dbCtxFactory
            .InTransactionAsync(
                async dbCtx =>
                {
                    dbCtx.AddRange(rows);

                    // The ids the movements name exist once the rows are inserted.
                    await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

                    movements.Record(dbCtx, Created(cause, rows, _ => Inventory(owner)));

                    return UnitOfWork.Commit(true);
                },
                ct
            )
            .ConfigureAwait(false);

        return [.. rows.Select(ToCustody)];
    }

    public async Task<CustodyFurni?> CreateInInventoryOnceAsync(
        PlayerId owner,
        FurniToMake furni,
        string reference,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (await GrantedAsync(owner, reference, ct).ConfigureAwait(false))
            return null;

        var row = NewRow(owner, furni);

        try
        {
            await dbCtxFactory
                .InTransactionAsync(
                    async dbCtx =>
                    {
                        dbCtx.Add(row);
                        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

                        dbCtx.Add(
                            new FurnitureGrantReceiptEntity
                            {
                                PlayerId = owner.Value,
                                Reference = reference,
                                FurnitureId = row.Id,
                                CreatedAt = DateTime.UtcNow,
                            }
                        );
                        movements.Record(dbCtx, Created(cause, [row], _ => Inventory(owner)));

                        return UnitOfWork.Commit(true);
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Another server granted it between the look and the write: its receipt won.
            if (await GrantedAsync(owner, reference, ct).ConfigureAwait(false))
                return null;

            throw;
        }

        return ToCustody(row);
    }

    private async Task<bool> GrantedAsync(PlayerId owner, string reference, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .FurnitureGrantReceipts.AsNoTracking()
            .AnyAsync(r => r.PlayerId == owner.Value && r.Reference == reference, ct)
            .ConfigureAwait(false);
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

        await dbCtxFactory
            .InTransactionAsync(
                async dbCtx =>
                {
                    dbCtx.AddRange(rows);

                    // The pair's ids exist only once the rows are inserted; the links are saved with the commit.
                    await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

                    for (var i = 0; i < rows.Count; i += 2)
                    {
                        rows[i].ExtraData = TeleportFurniture.PairExtraData(rows[i + 1].Id);
                        rows[i + 1].ExtraData = TeleportFurniture.PairExtraData(rows[i].Id);
                    }

                    movements.Record(dbCtx, Created(cause, rows, _ => Inventory(owner)));

                    return UnitOfWork.Commit(true);
                },
                ct
            )
            .ConfigureAwait(false);

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

        await dbCtxFactory
            .InTransactionAsync(
                async dbCtx =>
                {
                    dbCtx.Add(presentRow);

                    // The present's id exists only once its row is inserted.
                    await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

                    contentRow.ChestItemEntityId = presentRow.Id;
                    dbCtx.Add(contentRow);

                    await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

                    movements.Record(
                        dbCtx,
                        Created(
                            cause,
                            [presentRow, contentRow],
                            row =>
                                row == presentRow ? Inventory(owner) : Holder(owner, presentRow.Id)
                        )
                    );

                    return UnitOfWork.Commit(true);
                },
                ct
            )
            .ConfigureAwait(false);

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
                // Trades lock the offered wrapper, not its hidden content. Opening must take
                // that same lock and recheck the current owner before releasing anything.
                var parents = await dbCtx
                    .Furnitures.FromSqlRaw(
                        "SELECT * FROM furniture WHERE id = {0} FOR UPDATE",
                        presentId.Value
                    )
                    .AsNoTracking()
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var parent = parents.SingleOrDefault();
                if (
                    parent is null
                    || parent.PlayerEntityId != opener.Value
                    || parent.ChestItemEntityId is not null
                )
                    return UnitOfWork.Rollback<CustodyFurni?>(null);

                var row = await dbCtx
                    .Furnitures.Where(x => x.ChestItemEntityId == presentId.Value)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

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
        var ids = IdsOf(items);

        return dbCtxFactory.InTransactionAsync(
            async dbCtx =>
            {
                var updated = await InInventory(dbCtx, from, ids)
                    .ExecuteUpdateAsync(up => up.SetProperty(x => x.PlayerEntityId, to.Value), ct)
                    .ConfigureAwait(false);

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

                movements.Record(dbCtx, Moved(cause, items, Inventory(from), Inventory(to)));

                return UnitOfWork.Commit(true);
            },
            ct
        );
    }

    public Task<bool> PlaceInRoomAsync(
        RoomId roomId,
        RoomItemSnapshot item,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var owner = item.OwnerId;
        double z = item.Z;
        var wallOffset = WallOffsetOf(item) ?? 0;

        return dbCtxFactory.InTransactionAsync(
            async dbCtx =>
            {
                var placed = await InInventory(dbCtx, owner, [item.ObjectId.Value])
                    .ExecuteUpdateAsync(
                        up =>
                            up.SetProperty(x => x.RoomEntityId, roomId.Value)
                                .SetProperty(x => x.X, item.X)
                                .SetProperty(x => x.Y, item.Y)
                                .SetProperty(x => x.Z, z)
                                .SetProperty(x => x.Rotation, item.Rotation)
                                .SetProperty(x => x.WallOffset, wallOffset),
                        ct
                    )
                    .ConfigureAwait(false);

                if (placed == 0)
                    return UnitOfWork.Rollback(false);

                movements.Record(
                    dbCtx,
                    Movement(
                        cause,
                        0,
                        Furni(item.ObjectId.Value, item.DefinitionId),
                        Inventory(owner),
                        InRoom(owner, roomId, item)
                    )
                );

                return UnitOfWork.Commit(true);
            },
            ct
        );
    }

    public async Task<int> TakeOutOfRoomAsync(
        RoomId roomId,
        IReadOnlyList<RoomItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (items.Count == 0)
            return 0;

        var ids = items.Select(x => x.ObjectId.Value).ToList();

        var taken = await dbCtxFactory
            .InTransactionAsync(
                async dbCtx =>
                {
                    var rows = await dbCtx
                        .Furnitures.Where(x => ids.Contains(x.Id) && x.RoomEntityId == roomId.Value)
                        .ToDictionaryAsync(x => x.Id, ct)
                        .ConfigureAwait(false);

                    var at = DateTime.UtcNow;
                    var moved = new List<AssetMovement>(rows.Count);

                    foreach (var item in items)
                    {
                        if (!rows.TryGetValue(item.ObjectId.Value, out var row))
                            continue;

                        var leftBy = (PlayerId)row.PlayerEntityId;

                        // The spot and data it left with, so the inventory and a later restore see
                        // what the room last showed.
                        row.RoomEntityId = null;
                        row.PlayerEntityId = item.OwnerId.Value;
                        row.X = item.X;
                        row.Y = item.Y;
                        row.Z = item.Z;
                        row.Rotation = item.Rotation;
                        row.WallOffset = WallOffsetOf(item) ?? row.WallOffset;
                        row.ExtraData = item.ExtraData;

                        moved.Add(
                            Movement(
                                cause,
                                moved.Count,
                                Furni(row.Id, row.FurnitureDefinitionEntityId),
                                InRoom(leftBy, roomId, item),
                                Inventory(item.OwnerId),
                                at: at
                            )
                        );
                    }

                    movements.Record(dbCtx, moved);

                    return UnitOfWork.Commit(moved.Count);
                },
                ct
            )
            .ConfigureAwait(false);

        if (taken != items.Count)
            logger.LogWarning(
                "{Missing} of {Count} items taken out of room {RoomId} were no longer in it; skipped",
                items.Count - taken,
                items.Count,
                roomId
            );

        return taken;
    }

    public async Task<bool> DestroyInRoomAsync(
        TurboDbContext dbCtx,
        RoomId roomId,
        RoomItemSnapshot item,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var owner = await dbCtx
            .Furnitures.Where(x => x.Id == item.ObjectId.Value && x.RoomEntityId == roomId.Value)
            .Select(x => (int?)x.PlayerEntityId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (owner is null)
            return false;

        await dbCtx
            .Furnitures.Where(x => x.Id == item.ObjectId.Value)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        movements.Record(
            dbCtx,
            Movement(
                cause,
                0,
                Furni(item.ObjectId.Value, item.DefinitionId),
                InRoom((PlayerId)owner.Value, roomId, item),
                AssetPlace.Nowhere,
                new AssetSnapshot(item.DefinitionId, item.ExtraData)
            )
        );

        return true;
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

    /// <summary>A borrowed furni is known by its room and the id the room gave it.</summary>
    private static AssetRef Borrowed(RoomItemSnapshot item) =>
        new(AssetKind.BorrowedFurni, item.ObjectId.Value, item.DefinitionId, null);

    private static AssetPlace InRoom(PlayerId owner, RoomId roomId, RoomItemSnapshot item) =>
        InRoom(
            owner,
            roomId,
            new RoomPosition(item.X, item.Y, item.Z, item.Rotation, WallOffsetOf(item))
        );

    private static AssetPlace InRoom(PlayerId owner, RoomId roomId, RoomPosition position) =>
        new(owner.Value, PlaceKind.Room, roomId.Value, position);

    private static int? WallOffsetOf(RoomItemSnapshot item) =>
        item is RoomWallItemSnapshot wallItem ? wallItem.WallOffset : null;

    private static AssetPlace Holder(PlayerId owner, int holderId) =>
        new(owner.Value, PlaceKind.Holder, holderId, null);

    private static AssetPlace Holder(FurniHolder holder) => Holder(holder.HeldFor, holder.ItemId);

    private static List<int> IdsOf(IReadOnlyList<FurnitureItemSnapshot> items) =>
        [.. items.Select(x => x.ItemId.Value)];

    /// <summary>Furni that keep their row and change place, one movement each, in order.</summary>
    private static List<AssetMovement> Moved(
        MovementCause cause,
        IReadOnlyList<FurnitureItemSnapshot> items,
        AssetPlace from,
        AssetPlace to
    )
    {
        var at = DateTime.UtcNow;

        return
        [
            .. items.Select(
                (item, i) =>
                    Movement(
                        cause,
                        i,
                        Furni(item.ItemId.Value, item.Definition.Id),
                        from,
                        to,
                        at: at
                    )
            ),
        ];
    }

    /// <summary>Whom a borrowed furni belongs to: nobody, it is the club's.</summary>
    private static readonly PlayerId NOBODY = new(0);
}
