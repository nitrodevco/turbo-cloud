using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Database.Movements;
using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Inventory.Purse;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

/// <summary>
/// Owns a player's currency balances. Credits and debits are written through inside a database
/// transaction before the in-memory balances move, so an activation can be collected at any time
/// without a flush on deactivation.
/// </summary>
internal sealed partial class PlayerWalletGrain : Grain, IPlayerWalletGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly ICurrencyTypeProvider _currencyTypeProvider;
    private readonly IPlayerNoticeService _noticeService;
    private readonly IAssetMovementRecorder _movements;
    private readonly ILogger<IPlayerWalletGrain> _logger;

    private readonly PlayerWalletLiveState _state;

    private PlayerId PlayerId => _state.PlayerId;

    public PlayerWalletGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IGrainFactory grainFactory,
        ICurrencyTypeProvider currencyTypeProvider,
        IPlayerNoticeService noticeService,
        IAssetMovementRecorder movements,
        ILogger<IPlayerWalletGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _grainFactory = grainFactory;
        _currencyTypeProvider = currencyTypeProvider;
        _noticeService = noticeService;
        _movements = movements;
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
    /// balance itself; they share one transaction with the receipt of the call, and the first one
    /// the balance cannot cover rolls the others back. The same call made again under the same
    /// cause takes nothing and succeeds. Memory moves only after the commit. While recording, the
    /// movements of every currency taken commit in that same transaction.
    /// </summary>
    public Task<WalletDebitResult> TryDebitAsync(
        List<WalletDebitRequest> requests,
        MovementCause cause,
        CancellationToken ct
    ) => DebitOnceAsync(requests, DebitReceipt(OperationReceipt(cause)), cause, ct);

    /// <summary>
    /// <see cref="TryDebitAsync(List{WalletDebitRequest}, MovementCause, CancellationToken)"/>
    /// once per <paramref name="reference"/> for this player: asked again under the same
    /// reference, it takes nothing and succeeds, saying so. A credit under the same reference is
    /// not a repeat of it.
    /// </summary>
    public Task<WalletDebitResult> TryDebitAsync(
        List<WalletDebitRequest> requests,
        string reference,
        MovementCause cause,
        CancellationToken ct
    )
    {
        ThrowIfNotAReference(reference);

        return DebitOnceAsync(requests, DebitReceipt(reference), cause, ct);
    }

    private async Task<WalletDebitResult> DebitOnceAsync(
        List<WalletDebitRequest> requests,
        string reference,
        MovementCause cause,
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
        WalletCreditResult result;

        try
        {
            result = await ApplyOnceAsync(
                reference,
                async dbCtx =>
                {
                    foreach (var request in normalizedRequests)
                    {
                        current = request;

                        var cost = request.Amount;
                        var debited =
                            _state.CurrenciesByKind.TryGetValue(
                                request.CurrencyKind,
                                out var snapshot
                            )
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

                            return false;
                        }

                        // ChangedBy is the signed delta: a debit is negative, so the client shows
                        // it as spent rather than received.
                        updates.Add(
                            new WalletCurrencyUpdateSnapshot
                            {
                                CurrencyKind = request.CurrencyKind,
                                ChangedBy = -cost,
                                Amount = snapshot!.Amount - cost,
                            }
                        );
                    }

                    var at = DateTime.UtcNow;

                    _movements.Record(
                        dbCtx,
                        [
                            .. normalizedRequests.Select(
                                (request, seq) =>
                                    Debited(cause, seq, at, request.CurrencyKind, request.Amount)
                            ),
                        ]
                    );

                    return true;
                },
                ct
            );
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

            // A fault is not a short balance: the player is told the purchase failed, not that
            // they cannot afford it.
            return WalletDebitResult.Failed();
        }

        switch (result)
        {
            case WalletCreditResult.AlreadyApplied:
                return WalletDebitResult.Repeated();
            case WalletCreditResult.Rejected:
                return InsufficientBalance(current!);
        }

        foreach (var update in updates)
        {
            var snapshot = _state.CurrenciesByKind[update.CurrencyKind];

            _state.CurrenciesByKind[update.CurrencyKind] = snapshot with { Amount = update.Amount };
        }

        IReadOnlyList<IComposer> balances =
        [
            .. updates.Select(ToBalanceComposer).OfType<IComposer>(),
        ];

        // Several currencies moved at once: one batch to the one player, in order.
        if (balances.Count > 0)
            await _grainFactory.GetPlayerPresenceGrain(PlayerId).SendComposerAsync(balances, ct);

        return WalletDebitResult.Success();
    }

    /// <summary>
    /// The packet the client redraws a balance from; each currency has its own. Null for a
    /// currency the client has no balance display for.
    /// </summary>
    private static IComposer? ToBalanceComposer(WalletCurrencyUpdateSnapshot update) =>
        update.CurrencyKind.CurrencyType switch
        {
            CurrencyType.Credits => new CreditBalanceEventMessageComposer
            {
                Balance = update.Amount.ToString(CultureInfo.InvariantCulture),
            },
            CurrencyType.Emeralds => new EmeraldBalanceMessageComposer
            {
                EmeraldBalance = update.Amount,
            },
            CurrencyType.Silver => new SilverBalanceMessageComposer
            {
                SilverBalance = update.Amount,
            },
            CurrencyType.ActivityPoints => new HabboActivityPointNotificationMessageComposer
            {
                Amount = update.Amount,
                Change = update.ChangedBy,
                ActivityPointType = update.CurrencyKind.ActivityPointType ?? -1,
            },
            _ => null,
        };

    private static WalletDebitResult InsufficientBalance(WalletDebitRequest request) =>
        WalletDebitResult.InsufficientBalance(
            new WalletDebitFailure { CurrencyKind = request.CurrencyKind, Amount = request.Amount }
        );

    public async Task<bool> CreditAsync(
        CurrencyKind kind,
        int amount,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var (result, balance) = await CreditOnceAsync(
            kind,
            amount,
            OperationReceipt(cause),
            reward: false,
            cause,
            ct
        );

        if (result == WalletCreditResult.Applied && balance is not null)
            await _grainFactory.SendComposerToPlayerAsync(PlayerId, balance, ct);

        return result != WalletCreditResult.Rejected;
    }

    public async Task<bool> CreditRewardAsync(
        CurrencyKind kind,
        int amount,
        MovementCause cause,
        CancellationToken ct
    )
    {
        var (result, balance) = await CreditOnceAsync(
            kind,
            amount,
            OperationReceipt(cause),
            reward: true,
            cause,
            ct
        );

        if (result == WalletCreditResult.Applied)
        {
            // Both money and receipt are committed. Delivery cannot turn this into a failed grant.
            try
            {
                if (balance is not null)
                    await _grainFactory.TrySendComposerToPlayerAsync(PlayerId, balance, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Reward saved but balance update failed for player {PlayerId}",
                    PlayerId
                );
            }
            await DeliverPendingRewardsAsync(ct);
        }

        return result != WalletCreditResult.Rejected;
    }

    /// <summary>
    /// Adds to a balance once per <paramref name="reference"/>: the balance, the receipt and,
    /// while recording, the movement commit together, then memory moves. A reward also adds to
    /// the amount waiting to be announced. Answers the packet that redraws the balance.
    /// </summary>
    private async Task<(WalletCreditResult Result, IComposer? Balance)> CreditOnceAsync(
        CurrencyKind kind,
        int amount,
        string reference,
        bool reward,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (amount <= 0)
            return (WalletCreditResult.Rejected, null);

        PlayerCurrencyEntity? entity = null;

        var result = await ApplyOnceAsync(
            reference,
            async dbCtx =>
            {
                entity = await LoadOrAddBalanceAsync(dbCtx, kind, ct);

                if (entity is null || entity.Amount > int.MaxValue - amount)
                    return false;

                entity.Amount += amount;
                if (reward)
                    entity.PendingRewardAmount = checked(entity.PendingRewardAmount + amount);
                _movements.Record(dbCtx, Credited(cause, kind, amount));

                return true;
            },
            ct
        );

        if (result != WalletCreditResult.Applied)
            return (result, null);

        _state.CurrenciesByKind[kind] = entity!.ToSnapshot(kind);

        return (
            result,
            ToBalanceComposer(
                new WalletCurrencyUpdateSnapshot
                {
                    CurrencyKind = kind,
                    ChangedBy = amount,
                    Amount = entity.Amount,
                }
            )
        );
    }

    public Task<int> GetAmountForCurrencyAsync(CurrencyKind kind, CancellationToken ct) =>
        Task.FromResult(
            _state.CurrenciesByKind.TryGetValue(kind, out var snapshot) ? snapshot.Amount : 0
        );

    public Task<Dictionary<int, int>> GetActivityPointsAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, int>();

        // Every activity point type the hotel runs, at 0 until the player holds some: Habbo
        // lists all of them at login, a new account's too.
        foreach (var name in _currencyTypeProvider.GetEnabledCurrencyNames())
            if (
                _currencyTypeProvider.TryGetCurrencyKindByName(name, out var kind)
                && kind.CurrencyType == CurrencyType.ActivityPoints
            )
                result[kind.ActivityPointType ?? -1] = 0;

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
