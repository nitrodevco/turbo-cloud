using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Vault;
using Turbo.Primitives.Vault.Enums;

namespace Turbo.Catalog.Vault;

/// <summary>
/// The earnings vault (<see cref="IIncomeRewardService"/>): the rows of every registered source
/// and the earnings paid into the vault itself (<c>player_income_rewards</c>), and a claim paid
/// out by the sources of its category and from the vault's own rows into the purse.
/// </summary>
public sealed class IncomeRewardService(
    IEnumerable<IIncomeRewardSource> sources,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    TimeProvider time,
    IOptions<CatalogConfig> config,
    ILogger<IncomeRewardService> logger
) : IIncomeRewardService
{
    private readonly ImmutableArray<IIncomeRewardSource> _sources = [.. sources];

    /// <summary>The players a status is about to be pushed to.</summary>
    private readonly ConcurrentDictionary<PlayerId, byte> _statusPushes = new();

    public async Task<ImmutableArray<IncomeRewardSnapshot>> GetRewardsAsync(
        PlayerId player,
        CancellationToken ct
    )
    {
        var rewards = new List<IncomeRewardSnapshot>();

        foreach (var source in _sources)
            rewards.AddRange(await source.GetRewardsAsync(player, ct).ConfigureAwait(false));

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = dbCtx.ConfigureAwait(false);

        rewards.AddRange(
            (
                await dbCtx
                    .PlayerIncomeRewards.AsNoTracking()
                    .Where(x => x.PlayerEntityId == player.Value && x.ClaimedAt == null)
                    .OrderBy(x => x.Id)
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
            ).Select(x => new IncomeRewardSnapshot
            {
                RewardCategory = (VaultRewardCategoryType)x.Category,
                RewardType = (VaultRewardType)x.RewardType,
                Amount = x.Amount,
                ProductCode = x.ProductCode,
            })
        );

        // The official client capture lists the rows by category (daily gift, achievements, level
        // progression), each in the order it was earned.
        return [.. rewards.OrderBy(x => x.RewardCategory)];
    }

    public async Task<bool> ClaimAsync(
        PlayerId player,
        VaultRewardCategoryType category,
        CancellationToken ct
    )
    {
        var claiming = _sources
            .Where(x => category == VaultRewardCategoryType.All || x.Category == category)
            .ToList();

        var (held, claimed) = await ClaimHeldAsync(player, category, ct).ConfigureAwait(false);

        // Claim All with nothing waiting has nothing to pay; one category nobody pays into fails,
        // which Flash's EarningsView.onIncomeRewardClaimResponse answers by enabling its button again.
        if (claiming.Count == 0 && !held)
            return category == VaultRewardCategoryType.All;

        foreach (var source in claiming)
            claimed &= await source.ClaimAsync(player, ct).ConfigureAwait(false);

        return claimed;
    }

    public async Task<bool> DepositAsync(
        PlayerId player,
        VaultRewardCategoryType category,
        VaultRewardType type,
        int amount,
        string reference,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reference.Length, 128);

        if (amount <= 0 || CurrencyOf(type) is null || category == VaultRewardCategoryType.All)
            return false;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = dbCtx.ConfigureAwait(false);

        if (await HeldAsync(dbCtx, player, reference, ct).ConfigureAwait(false))
            return true;

        dbCtx.PlayerIncomeRewards.Add(
            new PlayerIncomeRewardEntity
            {
                PlayerEntityId = player.Value,
                Category = (int)category,
                RewardType = (int)type,
                Amount = amount,
                Reference = reference,
            }
        );

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            var check = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var checkScope = check.ConfigureAwait(false);

            // Another payer's retry deposited it first.
            if (await HeldAsync(check, player, reference, ct).ConfigureAwait(false))
                return true;

            throw;
        }

        // Flash's VaultController.onIncomeRewardNotificationMessageEvent (catalog/vault/
        // VaultController.as:139): "notification.earning.new" and the purse's dot.
        await grainFactory
            .TrySendComposerToPlayerAsync(
                player,
                new IncomeRewardNotificationMessageComposer { RewardCategory = category },
                ct
            )
            .ConfigureAwait(false);

        PushStatusSoon(player);

        return true;
    }

    /// <summary>
    /// The official server follows an earning's IncomeRewardNotification with the vault's
    /// IncomeRewardStatus nobody asked for, one per burst: 9 notifications in 7 bursts, 7 statuses
    /// 110-133 ms after each burst's first, no IncomeRewardStatus request from the client (official
    /// client capture 2026-10-13/17). Flash reads it like any status (VaultController.as:105): an
    /// open vault shows the new sums.
    /// </summary>
    private void PushStatusSoon(PlayerId player)
    {
        if (_statusPushes.TryAdd(player, 0))
            _ = PushStatusAsync(player);
    }

    private async Task PushStatusAsync(PlayerId player)
    {
        try
        {
            await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        Math.Max(0, config.Value.IncomeRewardStatusPushDelayMs)
                    ),
                    time
                )
                .ConfigureAwait(false);

            // An earning from here on starts the next push, so none is left out of a status.
            _statusPushes.TryRemove(player, out _);

            var rewards = await GetRewardsAsync(player, CancellationToken.None)
                .ConfigureAwait(false);

            await grainFactory
                .TrySendComposerToPlayerAsync(
                    player,
                    new IncomeRewardStatusMessageComposer { IncomeRewards = [.. rewards] },
                    CancellationToken.None
                )
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _statusPushes.TryRemove(player, out _);

            logger.LogWarning(
                e,
                "Earnings vault status for player {PlayerId} not sent",
                player.Value
            );
        }
    }

    /// <summary>
    /// Pays the vault's own rows of the category into the purse: the rows are first tagged with
    /// this claim, then each tagged category and reward type is credited once under a wallet
    /// reference made of that tag, and only then marked claimed. A claim that stopped before the
    /// mark is finished by the next one under the same reference, so nothing is paid twice.
    /// </summary>
    private async Task<(bool Held, bool Claimed)> ClaimHeldAsync(
        PlayerId player,
        VaultRewardCategoryType category,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = dbCtx.ConfigureAwait(false);

        var waiting = dbCtx.PlayerIncomeRewards.Where(x =>
            x.PlayerEntityId == player.Value && x.ClaimedAt == null
        );

        if (category != VaultRewardCategoryType.All)
            waiting = waiting.Where(x => x.Category == (int)category);

        var claim = Guid.NewGuid().ToString("N");

        await waiting
            .Where(x => x.ClaimReference == null)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.ClaimReference, claim), ct)
            .ConfigureAwait(false);

        var tagged = await waiting
            .Where(x => x.ClaimReference != null)
            .Select(x => new
            {
                x.ClaimReference,
                x.Category,
                x.RewardType,
                x.Amount,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (tagged.Count == 0)
            return (false, true);

        var wallet = grainFactory.GetPlayerWalletGrain(player);
        var claimed = true;

        foreach (var batch in tagged.GroupBy(x => x.ClaimReference!))
        {
            var paid = true;
            var cause = MovementCause.New(MovementCauses.VAULT_CLAIM, player.Value, batch.Key);
            var seq = 0;

            // One operation per claim; each payout is caused by its category, the source it pays.
            foreach (var payout in batch.GroupBy(x => (x.Category, x.RewardType)))
            {
                var result = await wallet
                    .CreditAsync(
                        CurrencyOf((VaultRewardType)payout.Key.RewardType)!.Value,
                        payout.Sum(x => x.Amount),
                        $"vault:{batch.Key}:{payout.Key.Category}:{payout.Key.RewardType}",
                        (
                            cause with
                            {
                                Cause = MovementCauses.VaultClaimOf(
                                    (VaultRewardCategoryType)payout.Key.Category
                                ),
                            }
                        ).StartingAt(seq++),
                        ct
                    )
                    .ConfigureAwait(false);

                paid &= result is WalletCreditResult.Applied or WalletCreditResult.AlreadyApplied;
            }

            if (!paid)
            {
                claimed = false;
                continue;
            }

            var now = time.GetUtcNow().UtcDateTime;

            await waiting
                .Where(x => x.ClaimReference == batch.Key)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.ClaimedAt, now), ct)
                .ConfigureAwait(false);
        }

        return (true, claimed);
    }

    private static Task<bool> HeldAsync(
        TurboDbContext dbCtx,
        PlayerId player,
        string reference,
        CancellationToken ct
    ) =>
        dbCtx.PlayerIncomeRewards.AnyAsync(
            x => x.PlayerEntityId == player.Value && x.Reference == reference,
            ct
        );

    /// <summary>
    /// The purse a row pays into: <c>EarningsView.onIncomeRewardDataReceived</c> shows type 0 as
    /// duckets (activity points type 0, the purse's <c>getActivityPointsForType(0)</c> it checks the
    /// ducket limit against) and type 1 as credits (catalog/vault/EarningsView.as:112-118, :309).
    /// </summary>
    private static CurrencyKind? CurrencyOf(VaultRewardType type) =>
        type switch
        {
            VaultRewardType.Duckets => CurrencyKind.ActivityPoints(0),
            VaultRewardType.Credits => CurrencyKind.Credits,
            _ => null,
        };
}
