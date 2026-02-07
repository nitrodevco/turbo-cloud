using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed class PlayerWalletGrain(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<PlayerWalletGrain> logger
) : Grain, IPlayerWalletGrain
{
    public async Task<WalletDebitResult> TryDebitAsync(
        ImmutableArray<WalletDebitRequest> requests,
        CancellationToken ct
    )
    {
        var normalizedRequests = NormalizeRequests(requests);
        if (normalizedRequests.Length == 0)
            return WalletDebitResult.Success();

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var playerId = (int)this.GetPrimaryKeyLong();

        foreach (var request in normalizedRequests)
        {
            var targetCurrency = await FindDebitCurrencyAsync(dbCtx, playerId, request, ct)
                .ConfigureAwait(false);
            if (targetCurrency is null)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);

                return WalletDebitResult.InsufficientBalance(
                    new WalletDebitFailure
                    {
                        CurrencyType = request.CurrencyType,
                        CurrencyKind = request.CurrencyKind,
                        ActivityPointType = request.ActivityPointType,
                    }
                );
            }

            var rowsAffected = await dbCtx
                .PlayerCurrencies.Where(x =>
                    x.Id == targetCurrency.Value.Id && x.Amount >= request.Amount
                )
                .ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.Amount, x => x.Amount - request.Amount),
                    ct
                )
                .ConfigureAwait(false);

            if (rowsAffected != 1)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);

                return WalletDebitResult.InsufficientBalance(
                    new WalletDebitFailure
                    {
                        CurrencyType = request.CurrencyType,
                        CurrencyKind = request.CurrencyKind,
                        ActivityPointType = request.ActivityPointType,
                    }
                );
            }
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return WalletDebitResult.Success();
    }

    public async Task RefundAsync(ImmutableArray<WalletDebitRequest> requests, CancellationToken ct)
    {
        var normalizedRequests = NormalizeRequests(requests);
        if (normalizedRequests.Length == 0)
            return;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var playerId = (int)this.GetPrimaryKeyLong();

        foreach (var request in normalizedRequests)
        {
            var targetCurrency = await FindRefundCurrencyAsync(dbCtx, playerId, request, ct)
                .ConfigureAwait(false);
            if (targetCurrency is null)
            {
                targetCurrency = await EnsureRefundCurrencyAsync(dbCtx, playerId, request, ct)
                    .ConfigureAwait(false);
                if (targetCurrency is null)
                {
                    logger.LogError(
                        "Wallet refund failed: playerId={PlayerId}, currencyType={CurrencyType}, amount={Amount}",
                        playerId,
                        request.CurrencyType,
                        request.Amount
                    );

                    throw new InvalidOperationException(
                        $"Unable to refund currency '{request.CurrencyType}' for player {playerId}."
                    );
                }
            }

            var rowsAffected = await dbCtx
                .PlayerCurrencies.Where(x => x.Id == targetCurrency.Value.Id)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.Amount, x => x.Amount + request.Amount),
                    ct
                )
                .ConfigureAwait(false);

            if (rowsAffected != 1)
            {
                logger.LogError(
                    "Wallet refund failed: playerId={PlayerId}, currencyType={CurrencyType}, amount={Amount}",
                    playerId,
                    request.CurrencyType,
                    request.Amount
                );

                throw new InvalidOperationException(
                    $"Unable to refund currency '{request.CurrencyType}' for player {playerId}."
                );
            }
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static ImmutableArray<WalletDebitRequest> NormalizeRequests(
        ImmutableArray<WalletDebitRequest> requests
    )
    {
        if (requests.IsDefaultOrEmpty)
            return [];

        var totals = new Dictionary<
            (
                string CurrencyType,
                WalletCurrencyKind Kind,
                int ActivityPointType,
                int? CurrencyTypeId
            ),
            long
        >(
            capacity: requests.Length,
            comparer: EqualityComparer<(string, WalletCurrencyKind, int, int?)>.Default
        );

        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.CurrencyType) || request.Amount <= 0)
                continue;

            var canonicalType = WalletCurrencyKeyMapper.ToCanonicalKey(
                request.CurrencyType,
                request.CurrencyKind,
                request.ActivityPointType
            );
            var key = (
                CurrencyType: canonicalType,
                Kind: request.CurrencyKind,
                request.ActivityPointType,
                request.CurrencyTypeId
            );

            if (!totals.TryAdd(key, request.Amount))
                totals[key] += request.Amount;
        }

        var builder = ImmutableArray.CreateBuilder<WalletDebitRequest>(totals.Count);
        foreach (var (key, amount) in totals)
        {
            if (amount > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(requests),
                    $"Requested debit for '{key.CurrencyType}' exceeds int range."
                );

            builder.Add(
                new WalletDebitRequest
                {
                    CurrencyType = key.CurrencyType,
                    CurrencyKind = key.Kind,
                    ActivityPointType = key.ActivityPointType,
                    CurrencyTypeId = key.CurrencyTypeId,
                    Amount = (int)amount,
                }
            );
        }

        return builder.MoveToImmutable();
    }

    private static async Task<(int Id, string Type)?> FindDebitCurrencyAsync(
        TurboDbContext dbCtx,
        int playerId,
        WalletDebitRequest request,
        CancellationToken ct
    )
    {
        if (request.CurrencyTypeId.HasValue)
        {
            var currencyTypeId = request.CurrencyTypeId.Value;
            var byTypeId = await dbCtx
                .PlayerCurrencies.Where(x =>
                    x.PlayerEntityId == playerId
                    && x.CurrencyTypeEntityId.HasValue
                    && x.CurrencyTypeEntityId.Value == currencyTypeId
                    && x.Amount >= request.Amount
                )
                .Select(x => new { x.Id, x.Type })
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (byTypeId is not null)
                return (byTypeId.Id, byTypeId.Type);
        }

        var keys = WalletCurrencyKeyMapper
            .GetEquivalentKeys(
                request.CurrencyType,
                request.CurrencyKind,
                request.ActivityPointType
            )
            .ToArray();

        var targetCurrency = await dbCtx
            .PlayerCurrencies.Where(x =>
                x.PlayerEntityId == playerId && keys.Contains(x.Type) && x.Amount >= request.Amount
            )
            .Select(x => new { x.Id, x.Type })
            .OrderByDescending(x => x.Type == request.CurrencyType)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return targetCurrency is null ? null : (targetCurrency.Id, targetCurrency.Type);
    }

    private static async Task<(int Id, string Type)?> FindRefundCurrencyAsync(
        TurboDbContext dbCtx,
        int playerId,
        WalletDebitRequest request,
        CancellationToken ct
    )
    {
        if (request.CurrencyTypeId.HasValue)
        {
            var currencyTypeId = request.CurrencyTypeId.Value;
            var byTypeId = await dbCtx
                .PlayerCurrencies.Where(x =>
                    x.PlayerEntityId == playerId
                    && x.CurrencyTypeEntityId.HasValue
                    && x.CurrencyTypeEntityId.Value == currencyTypeId
                )
                .Select(x => new { x.Id, x.Type })
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (byTypeId is not null)
                return (byTypeId.Id, byTypeId.Type);
        }

        var keys = WalletCurrencyKeyMapper
            .GetEquivalentKeys(
                request.CurrencyType,
                request.CurrencyKind,
                request.ActivityPointType
            )
            .ToArray();

        var targetCurrency = await dbCtx
            .PlayerCurrencies.Where(x => x.PlayerEntityId == playerId && keys.Contains(x.Type))
            .Select(x => new { x.Id, x.Type })
            .OrderByDescending(x => x.Type == request.CurrencyType)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return targetCurrency is null ? null : (targetCurrency.Id, targetCurrency.Type);
    }

    private static async Task<(int Id, string Type)?> EnsureRefundCurrencyAsync(
        TurboDbContext dbCtx,
        int playerId,
        WalletDebitRequest request,
        CancellationToken ct
    )
    {
        var createdCurrency = new PlayerCurrencyEntity
        {
            PlayerEntityId = playerId,
            Type = request.CurrencyType,
            Amount = 0,
            CurrencyTypeEntityId = request.CurrencyTypeId,
            PlayerEntity = null!,
        };

        dbCtx.PlayerCurrencies.Add(createdCurrency);

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
            return (createdCurrency.Id, createdCurrency.Type);
        }
        catch (DbUpdateException)
        {
            dbCtx.Entry(createdCurrency).State = EntityState.Detached;
            return await FindRefundCurrencyAsync(dbCtx, playerId, request, ct)
                .ConfigureAwait(false);
        }
    }
}
