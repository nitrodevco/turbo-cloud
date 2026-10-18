using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerWalletGrain
{
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
        await using (var db = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var receipt = await db.AchievementWalletReceipts.FindAsync(
                [PlayerId.Value, awardKey],
                ct
            );
            if (receipt is not null)
            {
                if (receipt.PayloadJson != payload)
                    throw new InvalidOperationException("Award receipt payload mismatch.");
                await HydrateAsync(ct);
                return true;
            }
        }
        PlayerCurrencyEntity? balance = null;
        var credited = await _dbCtxFactory.InTransactionAsync(
            async db =>
            {
                balance = await LoadOrAddBalanceAsync(db, kind, ct);
                if (balance is null || balance.Amount > int.MaxValue - amount)
                    return UnitOfWork.Rollback(false);
                balance.Amount += amount;
                balance.PendingRewardAmount = checked(balance.PendingRewardAmount + amount);
                db.AchievementWalletReceipts.Add(
                    new()
                    {
                        PlayerId = PlayerId.Value,
                        AwardKey = awardKey,
                        PayloadJson = payload,
                    }
                );
                _movements.Record(db, Credited(cause, kind, amount));
                return UnitOfWork.Commit(true);
            },
            ct
        );
        if (!credited)
            return false;
        _state.CurrenciesByKind[kind] = balance!.ToSnapshot(kind);
        var composer = ToBalanceComposer(
            new WalletCurrencyUpdateSnapshot
            {
                CurrencyKind = kind,
                Amount = balance.Amount,
                ChangedBy = amount,
            }
        );
        if (composer is not null)
            await _grainFactory.TrySendComposerToPlayerAsync(PlayerId, composer, ct);
        await DeliverPendingRewardsAsync(ct);
        return true;
    }
}
