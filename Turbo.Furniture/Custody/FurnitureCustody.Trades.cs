using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Extensions;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Trading;

namespace Turbo.Furniture.Custody;

public sealed partial class FurnitureCustody
{
    public async Task<bool> ExchangeTradeAsync(
        FurnitureTradeRequest request,
        MovementCause cause,
        CancellationToken ct
    )
    {
        ValidateTrade(request, cause);
        var hash = TradeHash(request, cause);
        var receiptInsertPending = false;

        try
        {
            return await dbCtxFactory
                .InTransactionAsync(
                    async dbCtx =>
                    {
                        var prior = await dbCtx
                            .FurnitureTradeReceipts.AsNoTracking()
                            .SingleOrDefaultAsync(x => x.OperationId == cause.OperationId, ct)
                            .ConfigureAwait(false);
                        if (prior is not null)
                        {
                            if (!ReceiptMatches(prior, hash))
                                throw new InvalidOperationException(
                                    "The trade operation id was already used for a different request."
                                );
                            return UnitOfWork.Rollback(true);
                        }

                        var initiatorIds = request.InitiatorItems.Select(ItemId).ToArray();
                        var partnerIds = request.PartnerItems.Select(ItemId).ToArray();
                        var receipt = new FurnitureTradeReceiptEntity
                        {
                            OperationId = cause.OperationId,
                            RequestHash = hash,
                            InitiatorId = request.InitiatorId.Value,
                            PartnerId = request.PartnerId.Value,
                            InitiatorItemIds = JsonSerializer.Serialize(initiatorIds),
                            PartnerItemIds = JsonSerializer.Serialize(partnerIds),
                            CreatedAt = DateTime.UtcNow,
                        };
                        dbCtx.FurnitureTradeReceipts.Add(receipt);
                        receiptInsertPending = true;
                        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
                        receiptInsertPending = false;

                        var allIds = initiatorIds.Concat(partnerIds).Order().ToArray();

                        // Read the participating rows in id order under MySQL locks. SQLite's
                        // behavior fixture strips FOR UPDATE; the MySQL tests exercise real contention.
                        var placeholders = string.Join(", ", allIds.Select((_, i) => $"{{{i}}}"));
                        var arguments = allIds.Cast<object>().ToArray();
                        var sql =
                            $"SELECT * FROM furniture WHERE id IN ({placeholders}) ORDER BY id FOR UPDATE";
                        var rows =
                            allIds.Length == 0
                                ? []
                                : await dbCtx
                                    .Furnitures.FromSqlRaw(sql, arguments)
                                    .AsNoTracking()
                                    .ToListAsync(ct)
                                    .ConfigureAwait(false);
                        if (
                            !ValidOffer(rows, request.InitiatorItems, request.InitiatorId.Value)
                            || !ValidOffer(rows, request.PartnerItems, request.PartnerId.Value)
                        )
                            return UnitOfWork.Rollback(false);

                        if (
                            !await TransferTradeSideAsync(
                                    dbCtx,
                                    initiatorIds,
                                    request.InitiatorId.Value,
                                    request.PartnerId.Value,
                                    ct
                                )
                                .ConfigureAwait(false)
                        )
                            return UnitOfWork.Rollback(false);
                        if (
                            !await TransferTradeSideAsync(
                                    dbCtx,
                                    partnerIds,
                                    request.PartnerId.Value,
                                    request.InitiatorId.Value,
                                    ct
                                )
                                .ConfigureAwait(false)
                        )
                            return UnitOfWork.Rollback(false);

                        var initiator = request.InitiatorId;
                        var partner = request.PartnerId;
                        movements.Record(
                            dbCtx,
                            [
                                .. Moved(
                                    cause,
                                    request.InitiatorItems,
                                    Inventory(initiator),
                                    Inventory(partner)
                                ),
                                .. Moved(
                                    cause.StartingAt(
                                        checked(cause.FirstSeq + request.InitiatorItems.Length)
                                    ),
                                    request.PartnerItems,
                                    Inventory(partner),
                                    Inventory(initiator)
                                ),
                            ]
                        );

                        return UnitOfWork.Commit(true);
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (receiptInsertPending)
        {
            var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);
            var existing = await db
                .FurnitureTradeReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OperationId == cause.OperationId, ct)
                .ConfigureAwait(false);
            if (existing is not null && ReceiptMatches(existing, hash))
                return true;
            if (existing is not null)
                throw new InvalidOperationException(
                    "The trade operation id was already used for a different request.",
                    ex
                );
            throw;
        }
    }

    private static bool ValidOffer(
        IReadOnlyCollection<FurnitureEntity> rows,
        ImmutableArray<FurnitureItemSnapshot> items,
        int owner
    ) =>
        items.All(item =>
            rows.Any(row =>
                row.Id == ItemId(item)
                && row.PlayerEntityId == owner
                && row.RoomEntityId is null
                && row.ChestItemEntityId is null
                && row.FurnitureDefinitionEntityId == item.Definition.Id
                && (row.ExtraData ?? "{}") == (item.ExtraData ?? "{}")
            )
        );

    private static async Task<bool> TransferTradeSideAsync(
        Turbo.Database.Context.TurboDbContext dbCtx,
        int[] ids,
        int from,
        int to,
        CancellationToken ct
    )
    {
        if (ids.Length == 0)
            return true;

        var changed = await dbCtx
            .Furnitures.Where(x =>
                ids.Contains(x.Id)
                && x.PlayerEntityId == from
                && x.RoomEntityId == null
                && x.ChestItemEntityId == null
                && x.FurnitureDefinitionEntity != null
                && x.FurnitureDefinitionEntity.CanTrade
            )
            .ExecuteUpdateAsync(up => up.SetProperty(x => x.PlayerEntityId, to), ct)
            .ConfigureAwait(false);
        return changed == ids.Length;
    }

    private static void ValidateTrade(FurnitureTradeRequest request, MovementCause cause)
    {
        if (request.InitiatorItems.IsDefault || request.PartnerItems.IsDefault)
            throw new ArgumentException("Both offers must be supplied.", nameof(request));
        if (
            request.InitiatorId.Value <= 0
            || request.PartnerId.Value <= 0
            || request.InitiatorId == request.PartnerId
        )
            throw new ArgumentException(
                "A furniture trade needs two distinct positive participants.",
                nameof(request)
            );
        if (
            string.IsNullOrWhiteSpace(cause.OperationId)
            || cause.OperationId.Length > 100
            || string.IsNullOrWhiteSpace(cause.Cause)
            || cause.ActorPlayerId < 0
            || cause.FirstSeq < 0
        )
            throw new ArgumentException(
                "The movement cause is invalid for a furniture trade.",
                nameof(cause)
            );

        var ids = request
            .InitiatorItems.Select(ItemId)
            .Concat(request.PartnerItems.Select(ItemId))
            .ToArray();
        if (ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException(
                "Trade item ids must be positive, unique, and disjoint.",
                nameof(request)
            );
        if (
            request.InitiatorItems.Any(x => x.OwnerId != request.InitiatorId || x.RoomId.Value > 0)
            || request.PartnerItems.Any(x => x.OwnerId != request.PartnerId || x.RoomId.Value > 0)
        )
            throw new ArgumentException(
                "Each offered item must name its submitting owner and inventory location.",
                nameof(request)
            );
    }

    private static string TradeHash(FurnitureTradeRequest request, MovementCause cause)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new System.IO.BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(request.InitiatorId.Value);
            writer.Write(request.PartnerId.Value);
            WriteOffer(writer, request.InitiatorItems);
            WriteOffer(writer, request.PartnerItems);
            writer.Write(cause.OperationId);
            writer.Write(cause.Cause);
            WriteNullable(writer, cause.CauseRef);
            writer.Write(cause.ActorPlayerId);
            writer.Write(cause.FirstSeq);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteOffer(
        System.IO.BinaryWriter writer,
        ImmutableArray<FurnitureItemSnapshot> items
    )
    {
        writer.Write(items.Length);
        foreach (var item in items)
        {
            writer.Write(ItemId(item));
            writer.Write(item.Definition.Id);
            writer.Write(item.ExtraData);
        }
    }

    private static void WriteNullable(System.IO.BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
            writer.Write(value);
    }

    private static int ItemId(FurnitureItemSnapshot item) => item.ItemId.Value;

    private static bool ReceiptMatches(FurnitureTradeReceiptEntity receipt, string hash) =>
        receipt.RequestHash == hash;
}
