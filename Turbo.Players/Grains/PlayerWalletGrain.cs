using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Players.Exceptions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

/// <summary>
/// Owns a player's currency balances. Credits and debits are written through inside a database
/// transaction before the in-memory balances move, so an activation can be collected at any time
/// without a flush on deactivation.
/// </summary>
internal sealed class PlayerWalletGrain(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ICurrencyTypeProvider currencyTypeProvider,
    IGrainFactory grainFactory,
    ILogger<IPlayerWalletGrain> logger
) : Grain, IPlayerWalletGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ICurrencyTypeProvider _currencyTypeProvider = currencyTypeProvider;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ILogger<IPlayerWalletGrain> _logger = logger;

    private readonly Dictionary<CurrencyKind, WalletCurrencySnapshot> _currenciesByKind = [];

    private PlayerId PlayerId => this.GetPlayerId();

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate the wallet of player {PlayerId}", PlayerId);

            throw;
        }
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
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
            await using var tx = await dbCtx.Database.BeginTransactionAsync(ct);

            var updates = new List<WalletCurrencyUpdateSnapshot>(normalizedRequests.Count);

            foreach (var request in normalizedRequests)
            {
                try
                {
                    var update = await ProcessDebitRequestAsync(dbCtx, request, ct);

                    if (update.ChangedBy != request.Amount)
                        throw new WalletDebitFailedException(
                            request.CurrencyKind,
                            request.Amount,
                            update.ChangedBy
                        );

                    updates.Add(update);
                }
                catch (Exception ex)
                {
                    // An insufficient balance is expected; anything else is a real failure.
                    if (ex is WalletDebitFailedException)
                        _logger.LogWarning(
                            "Player {PlayerId} could not be debited {Amount} of {CurrencyKind}",
                            PlayerId,
                            request.Amount,
                            request.CurrencyKind
                        );
                    else
                        _logger.LogError(
                            ex,
                            "Failed to debit {Amount} of {CurrencyKind} from player {PlayerId}",
                            request.Amount,
                            request.CurrencyKind,
                            PlayerId
                        );

                    await tx.RollbackAsync(ct);
                    await RollbackUpdatesAsync(updates, ct);

                    return WalletDebitResult.InsufficientBalance(
                        new WalletDebitFailure
                        {
                            CurrencyKind = request.CurrencyKind,
                            Amount = request.Amount,
                        }
                    );
                }
            }

            await dbCtx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var playerPresence = _grainFactory.GetPlayerPresenceGrain(this.GetPlayerId().Value);

            foreach (var update in updates)
                await playerPresence.OnCurrencyUpdateAsync(update, ct);
        }

        return WalletDebitResult.Success();
    }

    public Task RollbackUpdatesAsync(
        List<WalletCurrencyUpdateSnapshot> updates,
        CancellationToken ct
    )
    {
        if (updates.Count == 0)
            return Task.CompletedTask;
        foreach (var update in updates)
        {
            if (update is null || update.ChangedBy == 0)
                continue;

            if (_currenciesByKind.TryGetValue(update.CurrencyKind, out var snapshot))
            {
                _currenciesByKind[update.CurrencyKind] = snapshot with
                {
                    Amount = snapshot.Amount + update.ChangedBy,
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task<int> GetAmountForCurrencyAsync(CurrencyKind kind, CancellationToken ct) =>
        Task.FromResult(
            _currenciesByKind.TryGetValue(kind, out var snapshot) ? snapshot.Amount : 0
        );

    public Task<Dictionary<int, int>> GetActivityPointsAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, int>();

        foreach (var currency in _currenciesByKind.Values)
        {
            if (
                currency is null
                || currency.CurrencyKind.CurrencyType != CurrencyType.ActivityPoints
            )
                continue;

            result[currency.CurrencyKind.ActivityPointType ?? -1] = currency.Amount;
        }

        return Task.FromResult(result);
    }

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

    private async Task<WalletCurrencyUpdateSnapshot> ProcessDebitRequestAsync(
        TurboDbContext dbCtx,
        WalletDebitRequest request,
        CancellationToken ct
    )
    {
        var changedBy = 0;
        var currentAmount = 0;
        var cost = request.Amount;

        if (_currenciesByKind.TryGetValue(request.CurrencyKind, out var snapshot))
        {
            var entity = await dbCtx
                .PlayerCurrencies.Where(x =>
                    x.Id == snapshot.Id && x.PlayerEntityId == this.GetPlayerId().Value
                )
                .FirstOrDefaultAsync(ct);

            if (entity is not null)
            {
                currentAmount = entity.Amount;

                if ((cost > 0) && (currentAmount >= cost))
                {
                    changedBy = cost;
                    entity.Amount -= changedBy;
                    currentAmount = entity.Amount;
                }
            }

            _currenciesByKind[request.CurrencyKind] = snapshot with { Amount = currentAmount };
        }

        return new()
        {
            CurrencyKind = request.CurrencyKind,
            ChangedBy = changedBy,
            Amount = currentAmount,
        };
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        _currenciesByKind.Clear();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .PlayerCurrencies.AsNoTracking()
            .Where(x => x.PlayerEntityId == this.GetPlayerId().Value)
            .ToListAsync(ct);

        foreach (var entity in entities)
        {
            var currencyType = _currencyTypeProvider.GetCurrencyType(entity.CurrencyTypeEntityId);

            if (currencyType is null || !currencyType.Enabled)
                continue;

            var snapshot = new WalletCurrencySnapshot
            {
                Id = entity.Id,
                CurrencyKind = new CurrencyKind
                {
                    CurrencyType = currencyType.CurrencyType,
                    ActivityPointType = currencyType.ActivityPointType,
                },
                Amount = entity.Amount,
            };

            _currenciesByKind[snapshot.CurrencyKind] = snapshot;
        }
    }
}
