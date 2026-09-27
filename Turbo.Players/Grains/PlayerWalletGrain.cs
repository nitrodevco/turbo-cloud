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

    /// <summary>
    /// Takes every requested currency or none. Each currency is one conditional update
    /// (<c>amount = amount - cost WHERE amount &gt;= cost</c>), so the database refuses a short
    /// balance itself; with more than one currency they share a transaction, and the first one
    /// the balance cannot cover rolls the others back. Memory moves only after the commit.
    /// </summary>
    public async Task<WalletDebitResult> TryDebitAsync(
        List<WalletDebitRequest> requests,
        CancellationToken ct
    )
    {
        if (
            !TryNormalizeRequests(requests, out var normalizedRequests)
            || normalizedRequests.Count == 0
        )
            return WalletDebitResult.Success();

        var updates = new List<WalletCurrencyUpdateSnapshot>(normalizedRequests.Count);
        WalletDebitRequest? current = null;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
            // One statement is atomic on its own; only several need a transaction around them.
            await using var tx =
                normalizedRequests.Count > 1
                    ? await dbCtx.Database.BeginTransactionAsync(ct)
                    : null;

            foreach (var request in normalizedRequests)
            {
                current = request;

                var cost = request.Amount;
                var debited =
                    _state.CurrenciesByKind.TryGetValue(request.CurrencyKind, out var snapshot)
                    && await dbCtx
                        .PlayerCurrencies.Where(x =>
                            x.Id == snapshot.Id
                            && x.PlayerEntityId == PlayerId.Value
                            && x.Amount >= cost
                        )
                        .ExecuteUpdateAsync(
                            up => up.SetProperty(x => x.Amount, x => x.Amount - cost),
                            ct
                        ) == 1;

                if (!debited)
                {
                    // An insufficient balance is an answer, not a failure.
                    _logger.LogWarning(
                        "Player {PlayerId} could not be debited {Amount} of {CurrencyKind}",
                        PlayerId,
                        request.Amount,
                        request.CurrencyKind
                    );

                    if (tx is not null)
                        await tx.RollbackAsync(ct);

                    return InsufficientBalance(request);
                }

                // ChangedBy is the signed delta: a debit is negative, so the client shows it as
                // spent rather than received.
                updates.Add(
                    new WalletCurrencyUpdateSnapshot
                    {
                        CurrencyKind = request.CurrencyKind,
                        ChangedBy = -cost,
                        Amount = snapshot!.Amount - cost,
                    }
                );
            }

            if (tx is not null)
                await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            // Disposing the transaction rolled back whatever it had done.
            _logger.LogError(
                ex,
                "Failed to debit {Amount} of {CurrencyKind} from player {PlayerId}",
                current?.Amount,
                current?.CurrencyKind,
                PlayerId
            );

            return InsufficientBalance(current ?? normalizedRequests[0]);
        }

        foreach (var update in updates)
        {
            var snapshot = _state.CurrenciesByKind[update.CurrencyKind];

            _state.CurrenciesByKind[update.CurrencyKind] = snapshot with { Amount = update.Amount };
        }

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(PlayerId.Value);

        foreach (var update in updates)
            await playerPresence.OnCurrencyUpdateAsync(update, ct);

        return WalletDebitResult.Success();
    }

    private static WalletDebitResult InsufficientBalance(WalletDebitRequest request) =>
        WalletDebitResult.InsufficientBalance(
            new WalletDebitFailure { CurrencyKind = request.CurrencyKind, Amount = request.Amount }
        );

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
