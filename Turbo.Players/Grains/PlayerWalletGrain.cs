using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed class PlayerWalletGrain(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ICurrencyTypeProvider currencyTypeProvider
) : Grain, IPlayerWalletGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ICurrencyTypeProvider _currencyTypeProvider = currencyTypeProvider;

    private readonly Dictionary<CurrencyKind, WalletCurrencySnapshot> _currenciesByKind = [];

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await HydrateAsync(ct);
    }

    public async Task<WalletDebitResult> TryDebitAsync(
        List<WalletDebitRequest> requests,
        CancellationToken ct
    )
    {
        if (
            TryNormalizeRequests(requests, out var normalizedRequests)
            && normalizedRequests.Count > 0
        )
        {
            await using var dbCtx = await _dbCtxFactory
                .CreateDbContextAsync(ct)
                .ConfigureAwait(false);
            await using var tx = await dbCtx
                .Database.BeginTransactionAsync(ct)
                .ConfigureAwait(false);

            foreach (var request in normalizedRequests)
            {
                try
                {
                    if (!await ProcessDebitRequestAsync(dbCtx, request, ct))
                        throw new Exception("Failed to process debit request");
                }
                catch
                {
                    await tx.RollbackAsync(ct);

                    return WalletDebitResult.InsufficientBalance(
                        new WalletDebitFailure
                        {
                            CurrencyKind = request.CurrencyKind,
                            Amount = request.Amount,
                        }
                    );
                }
            }

            await tx.CommitAsync(ct);
        }

        return WalletDebitResult.Success();
    }

    public Task RefundAsync(ImmutableArray<WalletDebitRequest> requests, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public async Task<int> GetAmountForCurrencyAsync(CurrencyKind kind, CancellationToken ct)
    {
        if (_currenciesByKind.TryGetValue(kind, out var snapshot))
            return snapshot.Amount;

        return 0;
    }

    public async Task<Dictionary<int, int>> GetActivityPointsAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, int>();

        foreach (var currency in _currenciesByKind.Values)
        {
            if (currency is null || currency.CurrencyType != CurrencyType.ActivityPoints)
                continue;

            result[currency.ActivityPointType ?? -1] = currency.Amount;
        }

        return result;
    }

    public async Task<PlayerWalletSnapshot> GetSnapshotAsync(CancellationToken ct) =>
        new()
        {
            Credits = _currenciesByKind.TryGetValue(
                new CurrencyKind { CurrencyType = CurrencyType.Credits },
                out var credits
            )
                ? credits.Amount
                : 0,
            Emeralds = _currenciesByKind.TryGetValue(
                new CurrencyKind { CurrencyType = CurrencyType.Emeralds },
                out var emeralds
            )
                ? emeralds.Amount
                : 0,
            Silver = _currenciesByKind.TryGetValue(
                new CurrencyKind { CurrencyType = CurrencyType.Silver },
                out var silver
            )
                ? silver.Amount
                : 0,
            ActivityPointsByCategoryId = await GetActivityPointsAsync(ct),
        };

    private static bool TryNormalizeRequests(
        List<WalletDebitRequest> proposed,
        out List<WalletDebitRequest> normalized
    )
    {
        normalized = [];

        var totals = new Dictionary<CurrencyKind, int>(proposed.Count);

        foreach (var request in proposed)
        {
            if (request is null || request.Amount <= 0)
                continue;

            var cost = request.Amount;

            if (totals.TryGetValue(request.CurrencyKind, out var total))
                cost += total;

            totals[request.CurrencyKind] = cost;
        }

        foreach (var (kind, total) in totals)
        {
            if (total <= 0)
                continue;

            normalized.Add(new WalletDebitRequest { CurrencyKind = kind, Amount = total });
        }

        return true;
    }

    private async Task<bool> ProcessDebitRequestAsync(
        TurboDbContext dbCtx,
        WalletDebitRequest request,
        CancellationToken ct
    )
    {
        // TODO when a currency updates we need to send the packet

        if (request.Amount <= 0)
            return true;

        if (
            !_currenciesByKind.TryGetValue(request.CurrencyKind, out var currency)
            || currency.Amount < request.Amount
        )
            return false;

        var affectedRows = await dbCtx
            .PlayerCurrencies.Where(x =>
                x.Id == currency.Id
                && x.PlayerEntityId == (int)this.GetPrimaryKeyLong()
                && x.Amount >= request.Amount
            )
            .ExecuteUpdateAsync(
                update => update.SetProperty(x => x.Amount, x => x.Amount - request.Amount),
                ct
            );

        if (affectedRows != 1)
            return false;

        return true;
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        _currenciesByKind.Clear();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .PlayerCurrencies.AsNoTracking()
            .Where(x => x.PlayerEntityId == (int)this.GetPrimaryKeyLong())
            .ToListAsync(ct);

        foreach (var entity in entities)
        {
            var currencyType = _currencyTypeProvider.GetCurrencyType(entity.CurrencyTypeEntityId);

            if (currencyType is null || !currencyType.Enabled)
                continue;

            var snapshot = new WalletCurrencySnapshot
            {
                Id = entity.Id,
                CurrencyType = currencyType.CurrencyType,
                ActivityPointType = currencyType.ActivityPointType,
                Amount = entity.Amount,
            };
            var kind = new CurrencyKind
            {
                CurrencyType = snapshot.CurrencyType,
                ActivityPointType = snapshot.ActivityPointType,
            };

            _currenciesByKind[kind] = snapshot;
        }
    }
}
