using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerWalletGrain
{
    /// <summary>
    /// The award's receipt is written first, in the transaction of the balance, so its primary
    /// key answers a repeat, from this activation or another, as <see cref="ApplyOnceAsync"/>
    /// does (a receipt already there is read, not inserted again): a repeat credits nothing and
    /// succeeds; a repeat with another payload throws.
    /// </summary>
    public async Task<bool> CreditAchievementAsync(
        string awardKey,
        CurrencyKind kind,
        int amount,
        MovementCause cause,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(awardKey);
        if (amount <= 0 || !_currencyTypeProvider.TryGetCurrencyTypeId(kind, out _))
            return false;
        var payload = JsonSerializer.Serialize(
            new WalletDebitRequest { CurrencyKind = kind, Amount = amount }
        );
        PlayerCurrencyEntity? balance = null;
        WalletCreditResult result;
        try
        {
            result = await _dbCtxFactory.InTransactionAsync(
                async db =>
                {
                    if (
                        await db.AchievementWalletReceipts.FindAsync(
                            [PlayerId.Value, awardKey],
                            ct
                        ) is
                        { } receipt
                    )
                        return receipt.PayloadJson == payload
                            ? UnitOfWork.Rollback(WalletCreditResult.AlreadyApplied)
                            : throw new InvalidOperationException(
                                "Award receipt payload mismatch."
                            );

                    db.AchievementWalletReceipts.Add(
                        new()
                        {
                            PlayerId = PlayerId.Value,
                            AwardKey = awardKey,
                            PayloadJson = payload,
                        }
                    );
                    await db.SaveChangesAsync(ct);
                    balance = await LoadOrAddBalanceAsync(db, kind, ct);
                    if (balance is null || balance.Amount > int.MaxValue - amount)
                        return UnitOfWork.Rollback(WalletCreditResult.Rejected);
                    balance.Amount += amount;
                    balance.PendingRewardAmount = checked(balance.PendingRewardAmount + amount);
                    _movements.Record(db, Credited(cause, kind, amount));
                    return UnitOfWork.Commit(WalletCreditResult.Applied);
                },
                ct
            );
        }
        catch (DbUpdateException)
        {
            var receipt = await FindAchievementReceiptAsync(awardKey, ct);
            if (receipt is null)
                throw;
            if (receipt.PayloadJson != payload)
                throw new InvalidOperationException("Award receipt payload mismatch.");
            result = WalletCreditResult.AlreadyApplied;
        }
        if (result == WalletCreditResult.AlreadyApplied)
        {
            _logger.LogInformation(
                "Achievement award {AwardKey} of player {PlayerId} was already credited",
                awardKey,
                PlayerId
            );
            await HydrateAsync(ct);
            return true;
        }
        if (result != WalletCreditResult.Applied || balance is null)
            return false;
        _state.CurrenciesByKind[kind] = balance.ToSnapshot(kind);
        await AnnounceRewardAsync(
            ToBalanceComposer(
                new WalletCurrencyUpdateSnapshot
                {
                    CurrencyKind = kind,
                    Amount = balance.Amount,
                    ChangedBy = amount,
                }
            ),
            ct
        );
        return true;
    }

    private async Task<AchievementWalletReceiptEntity?> FindAchievementReceiptAsync(
        string awardKey,
        CancellationToken ct
    )
    {
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        return await db.AchievementWalletReceipts.FindAsync([PlayerId.Value, awardKey], ct);
    }
}
