using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
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
internal sealed class PlayerWalletGrain : Grain, IPlayerWalletGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly ICurrencyTypeProvider _currencyTypeProvider;
    private readonly ILogger<IPlayerWalletGrain> _logger;

    private readonly PlayerWalletLiveState _state;

    private PlayerId PlayerId => _state.PlayerId;

    public PlayerWalletGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IGrainFactory grainFactory,
        ICurrencyTypeProvider currencyTypeProvider,
        ILogger<IPlayerWalletGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _grainFactory = grainFactory;
        _currencyTypeProvider = currencyTypeProvider;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

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

                    if (update.ChangedBy != -request.Amount)
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

            var playerPresence = _grainFactory.GetPlayerPresenceGrain(PlayerId.Value);

            foreach (var update in updates)
                await playerPresence.OnCurrencyUpdateAsync(update, ct);
        }

        return WalletDebitResult.Success();
    }

    public async Task<bool> CreditAsync(CurrencyKind kind, int amount, CancellationToken ct)
    {
        if (amount <= 0)
            return false;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        int newAmount;

        if (_state.CurrenciesByKind.TryGetValue(kind, out var snapshot))
        {
            var entity = await dbCtx.PlayerCurrencies.FirstOrDefaultAsync(
                x => x.Id == snapshot.Id && x.PlayerEntityId == PlayerId.Value,
                ct
            );

            if (entity is null)
                return false;

            entity.Amount += amount;
            newAmount = entity.Amount;

            await dbCtx.SaveChangesAsync(ct);

            _state.CurrenciesByKind[kind] = snapshot with { Amount = newAmount };
        }
        else
        {
            // First time this player holds the currency: the row is created on the fly.
            if (!_currencyTypeProvider.TryGetCurrencyTypeId(kind, out var typeId))
            {
                _logger.LogWarning(
                    "Cannot credit {Amount} of {CurrencyKind} to player {PlayerId}: no such currency type",
                    amount,
                    kind,
                    PlayerId
                );

                return false;
            }

            var entity = new PlayerCurrencyEntity
            {
                PlayerEntityId = PlayerId.Value,
                CurrencyTypeEntityId = typeId,
                Amount = amount,
            };

            dbCtx.PlayerCurrencies.Add(entity);

            await dbCtx.SaveChangesAsync(ct);

            newAmount = amount;
            _state.CurrenciesByKind[kind] = entity.ToSnapshot(kind);
        }

        await _grainFactory
            .GetPlayerPresenceGrain(PlayerId)
            .OnCurrencyUpdateAsync(
                new WalletCurrencyUpdateSnapshot
                {
                    CurrencyKind = kind,
                    ChangedBy = amount,
                    Amount = newAmount,
                },
                ct
            );

        return true;
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

            if (_state.CurrenciesByKind.TryGetValue(update.CurrencyKind, out var snapshot))
            {
                _state.CurrenciesByKind[update.CurrencyKind] = snapshot with
                {
                    Amount = snapshot.Amount - update.ChangedBy,
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task<int> GetAmountForCurrencyAsync(CurrencyKind kind, CancellationToken ct) =>
        Task.FromResult(
            _state.CurrenciesByKind.TryGetValue(kind, out var snapshot) ? snapshot.Amount : 0
        );

    public Task<Dictionary<int, int>> GetActivityPointsAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, int>();

        foreach (var currency in _state.CurrenciesByKind.Values)
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

        if (_state.CurrenciesByKind.TryGetValue(request.CurrencyKind, out var snapshot))
        {
            var entity = await dbCtx
                .PlayerCurrencies.Where(x =>
                    x.Id == snapshot.Id && x.PlayerEntityId == PlayerId.Value
                )
                .FirstOrDefaultAsync(ct);

            if (entity is not null)
            {
                currentAmount = entity.Amount;

                if ((cost > 0) && (currentAmount >= cost))
                {
                    // ChangedBy is the signed delta: a debit is negative, so the client shows
                    // it as spent rather than received.
                    changedBy = -cost;
                    entity.Amount += changedBy;
                    currentAmount = entity.Amount;
                }
            }

            _state.CurrenciesByKind[request.CurrencyKind] = snapshot with
            {
                Amount = currentAmount,
            };
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
        _state.CurrenciesByKind.Clear();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .PlayerCurrencies.AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value)
            .ToListAsync(ct);

        foreach (var entity in entities)
        {
            var currencyType = _currencyTypeProvider.GetCurrencyType(entity.CurrencyTypeEntityId);

            if (currencyType is null || !currencyType.Enabled)
                continue;

            var snapshot = entity.ToSnapshot(
                new CurrencyKind
                {
                    CurrencyType = currencyType.CurrencyType,
                    ActivityPointType = currencyType.ActivityPointType,
                }
            );

            _state.CurrenciesByKind[snapshot.CurrencyKind] = snapshot;
        }
    }
}
