using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerWalletGrain
{
    // Not a plugin's: plugin keys start with a letter or digit (docs/wallet-idempotent-credit.md).
    private const string OPERATION_RECEIPT_PREFIX = "#op:";

    /// <summary>
    /// The receipt of one call of an operation: its id and the seq the call's movements start at.
    /// A call made again under the same cause, a retry, finds it and changes nothing; the calls of
    /// one action that each start at their own seq stay apart. An operation id too long for a
    /// receipt is kept as its hash.
    /// </summary>
    private static string OperationReceipt(MovementCause cause)
    {
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{cause.FirstSeq}:{cause.OperationId}"
        );
        var receipt = OPERATION_RECEIPT_PREFIX + key;

        return receipt.Length <= WalletCreditReference.MaxLength
            ? receipt
            : OPERATION_RECEIPT_PREFIX
                + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    // A debit's receipts have their own prefix, so a credit under a reference and a debit under
    // the same one (a plugin taking back its own grant) are two receipts, not a repeat.
    private const string DEBIT_RECEIPT_PREFIX = "#debit:";

    /// <summary>
    /// The receipt of a debit under <paramref name="key"/>: the plugin's reference or the cause's
    /// <see cref="OperationReceipt"/>, behind the debit prefix; kept as its hash when too long.
    /// </summary>
    private static string DebitReceipt(string key)
    {
        var receipt = DEBIT_RECEIPT_PREFIX + key;

        return receipt.Length <= WalletCreditReference.MaxLength
            ? receipt
            : DEBIT_RECEIPT_PREFIX
                + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    /// <summary>
    /// A plugin's reference: non-blank, short enough for a receipt, and not in the wallet's own
    /// receipt keys, which it could otherwise repeat or block.
    /// </summary>
    private static void ThrowIfNotAReference(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            reference.Length,
            WalletCreditReference.MaxLength
        );

        if (
            reference.StartsWith(OPERATION_RECEIPT_PREFIX, StringComparison.OrdinalIgnoreCase)
            || reference.StartsWith(DEBIT_RECEIPT_PREFIX, StringComparison.OrdinalIgnoreCase)
        )
            throw new ArgumentException(
                $"A wallet reference cannot start with {OPERATION_RECEIPT_PREFIX} or {DEBIT_RECEIPT_PREFIX}",
                nameof(reference)
            );
    }

    /// <summary>
    /// Runs one balance change with its receipt, in one transaction. The receipt is written first,
    /// so its primary key answers a repeat, from this activation or another. Applied when
    /// <paramref name="work"/> made the change, Rejected when it refused (nothing is kept, the
    /// receipt neither), AlreadyApplied when the receipt was there (memory is read again).
    /// </summary>
    private async Task<WalletCreditResult> ApplyOnceAsync(
        string reference,
        Func<TurboDbContext, Task<bool>> work,
        CancellationToken ct
    )
    {
        try
        {
            return await _dbCtxFactory.InTransactionAsync(
                async db =>
                {
                    db.WalletCreditReceipts.Add(
                        new WalletCreditReceiptEntity
                        {
                            PlayerId = PlayerId.Value,
                            Reference = reference,
                        }
                    );
                    await db.SaveChangesAsync(ct);

                    return await work(db)
                        ? UnitOfWork.Commit(WalletCreditResult.Applied)
                        : UnitOfWork.Rollback(WalletCreditResult.Rejected);
                },
                ct
            );
        }
        catch (DbUpdateException)
        {
            if (!await ReceiptExistsAsync(reference, ct))
                throw;

            _logger.LogInformation(
                "Wallet change {Reference} of player {PlayerId} was already applied",
                reference,
                PlayerId
            );
            await HydrateAsync(ct);

            return WalletCreditResult.AlreadyApplied;
        }
    }

    private async Task<bool> ReceiptExistsAsync(string reference, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        return await dbCtx.WalletCreditReceipts.AnyAsync(
            x => x.PlayerId == PlayerId.Value && x.Reference == reference,
            ct
        );
    }

    /// <summary>
    /// The player's row for <paramref name="kind"/>, tracked; a new one at 0 the first time the
    /// player holds the currency. Null when no such currency exists.
    /// </summary>
    private async Task<PlayerCurrencyEntity?> LoadOrAddBalanceAsync(
        TurboDbContext db,
        CurrencyKind kind,
        CancellationToken ct
    )
    {
        if (_state.CurrenciesByKind.TryGetValue(kind, out var snapshot))
            return await db.PlayerCurrencies.FirstOrDefaultAsync(
                x => x.Id == snapshot.Id && x.PlayerEntityId == PlayerId.Value,
                ct
            );

        if (!_currencyTypeProvider.TryGetCurrencyTypeId(kind, out var typeId))
        {
            _logger.LogWarning(
                "Cannot credit {CurrencyKind} to player {PlayerId}: no such currency type",
                kind,
                PlayerId
            );

            return null;
        }

        var balance = await db.PlayerCurrencies.FirstOrDefaultAsync(
            x => x.PlayerEntityId == PlayerId.Value && x.CurrencyTypeEntityId == typeId,
            ct
        );

        if (balance is null)
        {
            balance = new PlayerCurrencyEntity
            {
                PlayerEntityId = PlayerId.Value,
                CurrencyTypeEntityId = typeId,
                Amount = 0,
            };
            db.PlayerCurrencies.Add(balance);
        }

        return balance;
    }
}
