using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
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
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(awardKey);
        if (amount <= 0 || !_currencyTypeProvider.TryGetCurrencyTypeId(kind, out var typeId))
            return false;
        var payload = JsonSerializer.Serialize(
            new WalletDebitRequest { CurrencyKind = kind, Amount = amount }
        );
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var receipt = await db.AchievementWalletReceipts.FindAsync([PlayerId.Value, awardKey], ct);
        if (receipt is not null)
        {
            if (receipt.PayloadJson != payload)
                throw new InvalidOperationException("Award receipt payload mismatch.");
            await HydrateAsync(ct);
            return true;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var balance = await db.PlayerCurrencies.SingleOrDefaultAsync(
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
        if (balance.Amount > int.MaxValue - amount)
            return false;
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
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _state.CurrenciesByKind[kind] = balance.ToSnapshot(kind);
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
