using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Movements;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Operations.Movements;

/// <summary>
/// <see cref="IAssetReversalService"/> over the grains that own each asset: it never writes a
/// furni or a balance itself. The furni grains' own guarded writes are the compare and set; the
/// rows are read first only to answer why a move was refused. A receipt is claimed before the
/// move and settled after it, since the move is another grain's transaction.
/// </summary>
internal sealed class AssetReversalService(
    IGrainFactory grainFactory,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<AssetReversalService> logger
) : IAssetReversalService
{
    // Not a plugin's: plugin keys start with a letter or digit (docs/wallet-idempotent-credit.md).
    private const string WALLET_REFERENCE_PREFIX = "#rev:";

    private enum Move
    {
        Unsupported,
        Invalid,
        TransferFurni,
        DestroyFurni,
        RecreateFurni,
        DebitCurrency,
        CreditCurrency,
    }

    public async Task<ReversalResult> MoveAsync(ReversalRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        if (
            !await grainFactory.HasPermissionAsync(
                request.ActorPlayerId,
                PermissionNodes.Items.REVERSE,
                ct
            )
        )
            return Refused(request, ReversalOutcome.Forbidden);

        var move = Classify(request);

        switch (move)
        {
            case Move.Unsupported:
                return Refused(request, ReversalOutcome.Unsupported);
            case Move.Invalid:
                return Refused(request, ReversalOutcome.TargetInvalid);
        }

        var (receipt, resumed) = await ClaimAsync(request, ct);

        if (receipt.Outcome is ReversalOutcome.Applied)
            return new(ReversalOutcome.AlreadyApplied, receipt.AssetId);

        var cause = new MovementCause(
            request.OperationId,
            MovementCauses.REVERSAL,
            request.ReversedOperationId,
            request.ActorPlayerId
        );

        // A throw leaves the receipt unsettled, as a crash does: whether it moved is unknown.
        var result = move switch
        {
            Move.TransferFurni or Move.DestroyFurni => await MoveFurniAsync(
                request,
                move,
                resumed,
                cause,
                ct
            ),
            Move.RecreateFurni => await RecreateFurniAsync(request, resumed, cause, ct),
            _ => await MoveCurrencyAsync(request, move, cause, ct),
        };

        if (result.Succeeded)
        {
            await SettleAsync(receipt.Id, result.AssetId, ct);

            logger.LogInformation(
                "Reversal {OperationId} of {ReversedOperationId} by player {ActorPlayerId}: {Outcome} {AssetKind} {AssetId}",
                request.OperationId,
                request.ReversedOperationId,
                request.ActorPlayerId,
                result.Outcome,
                request.Asset.Kind,
                result.AssetId
            );

            return result;
        }

        // A furni made again by an unsettled call may exist: keep the receipt, never make it twice.
        if (!(move == Move.RecreateFurni && resumed))
            await ReleaseAsync(receipt.Id, ct);

        return Refused(request, result.Outcome);
    }

    private static void Validate(ReversalRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            request.OperationId.Length,
            AssetMovement.MAX_OPERATION_ID_LENGTH
        );
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ReversedOperationId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            request.ReversedOperationId.Length,
            AssetMovement.MAX_CAUSE_REF_LENGTH
        );
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            request.Reason.Length,
            ReversalRequest.MAX_REASON_LENGTH
        );
    }

    private static Move Classify(ReversalRequest request)
    {
        var (expected, target) = (request.Expected.Kind, request.Target.Kind);

        switch (request.Asset.Kind)
        {
            case AssetKind.Furni:
                if (expected == PlaceKind.Inventory && target == PlaceKind.Inventory)
                    return
                        request.Expected.OwnerPlayerId > 0
                        && request.Target.OwnerPlayerId > 0
                        && request.Expected.OwnerPlayerId != request.Target.OwnerPlayerId
                        ? Move.TransferFurni
                        : Move.Invalid;

                if (expected == PlaceKind.Inventory && target == PlaceKind.None)
                    return request.Expected.OwnerPlayerId > 0 ? Move.DestroyFurni : Move.Invalid;

                if (expected == PlaceKind.None && target == PlaceKind.Inventory)
                    return request.Recreate is not null && request.Target.OwnerPlayerId > 0
                        ? Move.RecreateFurni
                        : Move.Invalid;

                return Move.Unsupported;

            case AssetKind.Currency:
                if (
                    request.Asset.Currency is null
                    || request.Amount <= 0
                    || request.Amount > int.MaxValue
                )
                    return Move.Invalid;

                if (expected == PlaceKind.Wallet && target == PlaceKind.None)
                    return request.Expected.OwnerPlayerId > 0 ? Move.DebitCurrency : Move.Invalid;

                if (expected == PlaceKind.None && target == PlaceKind.Wallet)
                    return request.Target.OwnerPlayerId > 0 ? Move.CreditCurrency : Move.Invalid;

                return Move.Unsupported;

            // A borrowed furni is the club's, and pets and bots wait on their room's return.
            default:
                return Move.Unsupported;
        }
    }

    /// <summary>
    /// Inventory -> inventory or inventory -> none through the holder's inventory grain, whose
    /// guarded write refuses an item no longer held there. Refused, the item may already stand
    /// at the target because a call of this operation moved it: that is answered as done.
    /// </summary>
    private async Task<ReversalResult> MoveFurniAsync(
        ReversalRequest request,
        Move move,
        bool resumed,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var id = request.Asset.Id;

        if (id is <= 0 or > int.MaxValue)
            return new(ReversalOutcome.NotWhereExpected, id);

        if (
            move == Move.TransferFurni
            && !await PlayerExistsAsync(request.Target.OwnerPlayerId, ct)
        )
            return new(ReversalOutcome.TargetInvalid, id);

        var itemId = (int)id;

        if (await IsFurniAtAsync(request.Asset, request.Expected, ct))
        {
            var inventory = grainFactory.GetInventoryGrain(request.Expected.OwnerPlayerId);
            var moved =
                move == Move.TransferFurni
                    ? await inventory.TransferFurnitureAsync(
                        [itemId],
                        request.Target.OwnerPlayerId,
                        cause,
                        ct
                    )
                    : await inventory.ConsumeFurnitureAsync(itemId, cause, ct) is not null;

            if (moved)
                return new(ReversalOutcome.Applied, id);
        }

        return
            await IsFurniAtAsync(request.Asset, request.Target, ct)
            && (resumed || await IsSettledAsync(request.OperationId, ct))
            ? new(ReversalOutcome.AlreadyApplied, id)
            : new(ReversalOutcome.NotWhereExpected, id);
    }

    /// <summary>
    /// None -> inventory: a new row from the snapshot, given by the inventory grain. The old id,
    /// when there was one, must hold no row. A call that finds an unsettled receipt cannot tell
    /// whether the furni was made, so it refuses rather than make a second one.
    /// </summary>
    private async Task<ReversalResult> RecreateFurniAsync(
        ReversalRequest request,
        bool resumed,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (resumed)
        {
            logger.LogWarning(
                "Reversal {OperationId} found its own unsettled receipt: the furni it makes may exist already; not made again",
                request.OperationId
            );

            return new(ReversalOutcome.NotWhereExpected);
        }

        if (request.Asset.Id != 0 && !await IsFurniAtAsync(request.Asset, AssetPlace.Nowhere, ct))
            return new(ReversalOutcome.NotWhereExpected, request.Asset.Id);

        if (!await PlayerExistsAsync(request.Target.OwnerPlayerId, ct))
            return new(ReversalOutcome.TargetInvalid);

        var made = await grainFactory
            .GetInventoryGrain(request.Target.OwnerPlayerId)
            .GrantFurnitureAsync(
                request.Recreate!.DefinitionId,
                request.Recreate.ExtraData,
                cause,
                ct
            );

        return made is null
            ? new(ReversalOutcome.TargetInvalid)
            : new(ReversalOutcome.Applied, made.ItemId.Value);
    }

    /// <summary>
    /// Wallet -> none or none -> wallet: a referenced debit or credit, so the wallet itself pays
    /// a retried reversal once. A balance too short to take back is not where expected.
    /// </summary>
    private async Task<ReversalResult> MoveCurrencyAsync(
        ReversalRequest request,
        Move move,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var kind = request.Asset.Currency!.Value;
        var amount = (int)request.Amount;
        var reference = WalletReference(request.OperationId);

        if (move == Move.DebitCurrency)
        {
            var debit = await grainFactory
                .GetPlayerWalletGrain(request.Expected.OwnerPlayerId)
                .TryDebitAsync(
                    [new WalletDebitRequest { CurrencyKind = kind, Amount = amount }],
                    reference,
                    cause,
                    ct
                );

            return new(
                !debit.Succeeded ? ReversalOutcome.NotWhereExpected
                : debit.AlreadyApplied ? ReversalOutcome.AlreadyApplied
                : ReversalOutcome.Applied
            );
        }

        if (!await PlayerExistsAsync(request.Target.OwnerPlayerId, ct))
            return new(ReversalOutcome.TargetInvalid);

        var credit = await grainFactory
            .GetPlayerWalletGrain(request.Target.OwnerPlayerId)
            .CreditAsync(kind, amount, reference, cause, ct);

        return new(
            credit switch
            {
                WalletCreditResult.Applied => ReversalOutcome.Applied,
                WalletCreditResult.AlreadyApplied => ReversalOutcome.AlreadyApplied,
                _ => ReversalOutcome.TargetInvalid,
            }
        );
    }

    /// <summary>The wallet receipt of a reversal; an operation id too long for one is kept as its hash.</summary>
    private static string WalletReference(string operationId)
    {
        var reference = WALLET_REFERENCE_PREFIX + operationId;

        return reference.Length <= WalletCreditReference.MaxLength
            ? reference
            : WALLET_REFERENCE_PREFIX
                + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(operationId)));
    }

    /// <summary>
    /// Whether the furni row is at <paramref name="place"/>: its owner, in no room and no holder,
    /// for an inventory; no row at all for nowhere. Its definition must be the asset's, when given.
    /// </summary>
    private async Task<bool> IsFurniAtAsync(AssetRef asset, AssetPlace place, CancellationToken ct)
    {
        if (asset.Id is <= 0 or > int.MaxValue)
            return place.Kind == PlaceKind.None;

        var id = (int)asset.Id;

        await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

        var row = await db
            .Furnitures.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.PlayerEntityId,
                x.FurnitureDefinitionEntityId,
                x.RoomEntityId,
                x.ChestItemEntityId,
            })
            .FirstOrDefaultAsync(ct);

        return place.Kind switch
        {
            PlaceKind.None => row is null,
            PlaceKind.Inventory => row is not null
                && row.PlayerEntityId == place.OwnerPlayerId
                && row.RoomEntityId is null
                && row.ChestItemEntityId is null
                && (
                    asset.DefinitionId == 0 || row.FurnitureDefinitionEntityId == asset.DefinitionId
                ),
            _ => false,
        };
    }

    private async Task<bool> PlayerExistsAsync(int playerId, CancellationToken ct)
    {
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

        return await db.Players.AnyAsync(x => x.Id == playerId, ct);
    }

    /// <summary>
    /// The operation's receipt: a new one, unsettled, or the one an earlier call of the operation
    /// left (resumed). Its unique operation id settles a race with another call.
    /// </summary>
    private async Task<(AssetReversalReceiptEntity Receipt, bool Resumed)> ClaimAsync(
        ReversalRequest request,
        CancellationToken ct
    )
    {
        var receipt = new AssetReversalReceiptEntity
        {
            OperationId = request.OperationId,
            ReversedOperationId = request.ReversedOperationId,
            Actor = request.ActorPlayerId,
            Reason = request.Reason,
            AssetKind = request.Asset.Kind,
            AssetId = request.Asset.Id,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

            db.AssetReversalReceipts.Add(receipt);
            await db.SaveChangesAsync(ct);

            return (receipt, false);
        }
        catch (DbUpdateException)
        {
            await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

            var existing = await db
                .AssetReversalReceipts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OperationId == request.OperationId, ct);

            if (existing is null)
                throw;

            return (existing, true);
        }
    }

    private async Task<bool> IsSettledAsync(string operationId, CancellationToken ct)
    {
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

        return await db.AssetReversalReceipts.AnyAsync(
            x => x.OperationId == operationId && x.Outcome == ReversalOutcome.Applied,
            ct
        );
    }

    private async Task SettleAsync(long receiptId, long assetId, CancellationToken ct)
    {
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

        await db
            .AssetReversalReceipts.Where(x => x.Id == receiptId)
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(x => x.Outcome, (ReversalOutcome?)ReversalOutcome.Applied)
                        .SetProperty(x => x.AssetId, assetId),
                ct
            );
    }

    /// <summary>A refused reversal keeps no receipt, so the operation can be asked again.</summary>
    private async Task ReleaseAsync(long receiptId, CancellationToken ct)
    {
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

        await db
            .AssetReversalReceipts.Where(x => x.Id == receiptId && x.Outcome == null)
            .ExecuteDeleteAsync(ct);
    }

    private ReversalResult Refused(ReversalRequest request, ReversalOutcome outcome)
    {
        logger.LogWarning(
            "Reversal {OperationId} of {ReversedOperationId} by player {ActorPlayerId} refused: {Outcome} ({AssetKind} {AssetId}, {ExpectedKind} -> {TargetKind})",
            request.OperationId,
            request.ReversedOperationId,
            request.ActorPlayerId,
            outcome,
            request.Asset.Kind,
            request.Asset.Id,
            request.Expected.Kind,
            request.Target.Kind
        );

        return new(outcome, request.Asset.Id);
    }
}
