using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Operations.OnlineRewards;

/// <summary>
/// Pays the players logged in to this server for their time online (<see cref="OnlineRewardsConfig"/>).
/// One loop per server looks every <see cref="OnlineRewardsConfig.CheckSeconds"/> at who is
/// online and whose next interval since login has come; each of those who is not idle is paid
/// through their wallet like <c>:give</c>, so the client is sent the balance packet it redraws
/// the purse from and the ledger records a <see cref="MovementCauses.REWARD_ONLINE"/> movement.
/// A clock is kept per login and dropped with it; nothing runs per player between payouts.
/// </summary>
public sealed class OnlineRewardService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptionsMonitor<OnlineRewardsConfig> config,
    IOptionsMonitor<OperationsConfig> operationsConfig,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    ICurrencyTypeProvider currencyTypeProvider,
    TimeProvider timeProvider,
    ILogger<OnlineRewardService> logger
) : BackgroundService
{
    // Only the loop touches these: one sweep at a time.
    private readonly Dictionary<PlayerId, OnlineRewardClock> _clocks = [];
    private readonly HashSet<PlayerId> _seen = [];
    private readonly HashSet<string> _unknownCurrencies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A rule as paid: its currency resolved, the hotel's idle and sleep settings where it has none.</summary>
    private sealed record Payout(
        string Currency,
        CurrencyKind Kind,
        int CurrencyTypeId,
        TimeSpan Interval,
        int Amount,
        int HcBonus,
        int DailyCap,
        bool InRoomOnly,
        int IdleMinutes,
        bool SkipSleeping
    );

    /// <summary>Where a player is when a payout comes round, read once for all their payouts.</summary>
    private sealed class Whereabouts(IGrainFactory grainFactory, PlayerId playerId)
    {
        private RoomPointerSnapshot? _room;
        private bool? _asleep;

        public async ValueTask<bool> InRoomAsync(CancellationToken ct) =>
            (await RoomAsync(ct)).RoomId.Value > 0;

        /// <summary>Whether the avatar sleeps in the room the player is in. Reading it does not wake it.</summary>
        public async ValueTask<bool> AsleepAsync(CancellationToken ct)
        {
            if (_asleep is { } asleep)
                return asleep;

            var room = await RoomAsync(ct);

            if (room.RoomId.Value <= 0)
                return (_asleep = false).Value;

            var avatar = await grainFactory
                .GetRoomGrain(room.RoomId)
                .GetAvatarPositionAsync(playerId, ct);

            return (_asleep = avatar?.IsIdle == true).Value;
        }

        private async ValueTask<RoomPointerSnapshot> RoomAsync(CancellationToken ct) =>
            _room ??=
                await grainFactory.GetPlayerPresenceGrain(playerId).GetActiveRoomAsync(ct)
                ?? new RoomPointerSnapshot { RoomId = -1, ActiveSinceUtc = default };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(Math.Max(1, config.CurrentValue.CheckSeconds)),
            timeProvider
        );

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await SweepAsync(Online(), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Online rewards could not be paid this round");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private List<OnlinePlayer> Online()
    {
        var online = new List<OnlinePlayer>();

        foreach (var session in sessionGateway.GetSessions())
        {
            var playerId = sessionGateway.GetPlayerId(session.SessionKey);

            if (playerId.Value > 0)
                online.Add(
                    new OnlinePlayer(
                        playerId,
                        session.SessionKey,
                        session.StartTime.UtcDateTime,
                        session.LastActiveUtc
                    )
                );
        }

        return online;
    }

    /// <summary>
    /// One look at who is online: starts a clock for each new login, drops those gone, and pays
    /// whoever has a payout due. Answers how many payouts were made.
    /// </summary>
    internal async Task<int> SweepAsync(
        IReadOnlyCollection<OnlinePlayer> online,
        CancellationToken ct
    )
    {
        var current = config.CurrentValue;
        var payouts = current.Enabled ? Resolve(current) : [];

        if (payouts.Count == 0)
        {
            _clocks.Clear();

            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var paying = payouts.Select(x => x.CurrencyTypeId).ToHashSet();
        var due = new List<(OnlinePlayer Player, List<Payout> Payouts)>();
        _seen.Clear();

        foreach (var player in online)
        {
            _seen.Add(player.PlayerId);

            // A new connection is a new login, and its clock starts over.
            if (
                !_clocks.TryGetValue(player.PlayerId, out var clock)
                || clock.Session != player.Session
            )
                _clocks[player.PlayerId] = clock = new OnlineRewardClock(
                    player.Session,
                    player.OnlineSinceUtc
                );

            clock.Keep(paying);

            List<Payout>? mine = null;

            foreach (var payout in payouts)
                if (clock.TakeDue(payout.CurrencyTypeId, payout.Interval, now))
                    (mine ??= []).Add(payout);

            if (mine is not null)
                due.Add((player, mine));
        }

        foreach (var gone in _clocks.Keys.Where(x => !_seen.Contains(x)).ToList())
            _clocks.Remove(gone);

        if (due.Count == 0)
            return 0;

        var paid = 0;

        await Parallel.ForEachAsync(
            due,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, current.MaxConcurrentPayouts),
                CancellationToken = ct,
            },
            async (x, token) =>
            {
                var count = await PayAsync(x.Player, x.Payouts, now, token);

                Interlocked.Add(ref paid, count);
            }
        );

        return paid;
    }

    /// <summary>The rules that pay: on, with an amount and an interval, in a currency the hotel has; the first per currency.</summary>
    private List<Payout> Resolve(OnlineRewardsConfig current)
    {
        var payouts = new List<Payout>();

        foreach (var rule in current.Rules)
        {
            if (
                !rule.Enabled
                || rule.Amount <= 0
                || rule.IntervalMinutes <= 0
                || string.IsNullOrWhiteSpace(rule.Currency)
            )
                continue;

            if (
                !currencyTypeProvider.TryGetCurrencyKindByName(rule.Currency, out var kind)
                || !currencyTypeProvider.TryGetCurrencyTypeId(kind, out var typeId)
            )
            {
                if (_unknownCurrencies.Add(rule.Currency))
                    logger.LogWarning(
                        "An online reward pays {Currency}, which is not a currency the hotel has; it pays nothing",
                        rule.Currency
                    );

                continue;
            }

            if (payouts.Any(x => x.CurrencyTypeId == typeId))
                continue;

            payouts.Add(
                new Payout(
                    rule.Currency,
                    kind,
                    typeId,
                    TimeSpan.FromMinutes(rule.IntervalMinutes),
                    rule.Amount,
                    Math.Max(0, rule.HcBonus),
                    Math.Max(0, rule.DailyCap),
                    rule.InRoomOnly,
                    Math.Max(0, rule.IdleMinutes ?? current.IdleMinutes),
                    rule.SkipSleeping ?? current.SkipSleeping
                )
            );
        }

        return payouts;
    }

    /// <summary>
    /// Pays one player what is due to them, but no payout whose rule they miss it by: idle, asleep
    /// or not in a room. Answers how many payouts were made.
    /// </summary>
    private async Task<int> PayAsync(
        OnlinePlayer player,
        List<Payout> due,
        DateTime now,
        CancellationToken ct
    )
    {
        try
        {
            var where = new Whereabouts(grainFactory, player.PlayerId);
            var payouts = new List<Payout>(due.Count);

            foreach (var payout in due)
            {
                if (
                    payout.IdleMinutes > 0
                    && now - player.LastActiveUtc >= TimeSpan.FromMinutes(payout.IdleMinutes)
                )
                    continue;

                if (payout.InRoomOnly && !await where.InRoomAsync(ct))
                    continue;

                if (payout.SkipSleeping && await where.AsleepAsync(ct))
                    continue;

                payouts.Add(payout);
            }

            if (payouts.Count == 0)
                return 0;

            var club =
                payouts.Any(x => x.HcBonus > 0)
                && await grainFactory.HasActiveClubAsync(player.PlayerId, ct);
            var max = operationsConfig.CurrentValue.MaxCurrencyAmount;
            var today = DateOnly.FromDateTime(now);
            var paid = 0;

            foreach (var payout in payouts)
            {
                var amount = (int)Math.Min(max, (long)payout.Amount + (club ? payout.HcBonus : 0));

                if (payout.DailyCap > 0)
                    amount = await ReserveAsync(player.PlayerId, payout, amount, today, ct);

                if (amount <= 0)
                    continue;

                var credited = await grainFactory
                    .GetPlayerWalletGrain(player.PlayerId)
                    .CreditAsync(
                        payout.Kind,
                        amount,
                        MovementCause.New(
                            MovementCauses.REWARD_ONLINE,
                            player.PlayerId.Value,
                            payout.Currency
                        ),
                        ct
                    );

                if (credited)
                    paid++;
                else if (payout.DailyCap > 0)
                    await ReserveAsync(player.PlayerId, payout, -amount, today, ct);
            }

            return paid;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Player {PlayerId} could not be paid their online reward",
                player.PlayerId
            );

            return 0;
        }
    }

    /// <summary>
    /// Counts <paramref name="amount"/> toward the player's day of this currency, up to its cap,
    /// and answers how much was counted: what may be paid. A negative amount gives back what a
    /// failed payout counted.
    /// </summary>
    private async Task<int> ReserveAsync(
        PlayerId playerId,
        Payout payout,
        int amount,
        DateOnly today,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var row = await dbCtx.PlayerOnlineRewards.FirstOrDefaultAsync(
            x =>
                x.PlayerEntityId == playerId.Value
                && x.CurrencyTypeEntityId == payout.CurrencyTypeId,
            ct
        );

        if (row is null)
        {
            row = new PlayerOnlineRewardEntity
            {
                PlayerEntityId = playerId.Value,
                CurrencyTypeEntityId = payout.CurrencyTypeId,
                Day = today,
            };
            dbCtx.PlayerOnlineRewards.Add(row);
        }
        else if (row.Day != today)
        {
            row.Day = today;
            row.Amount = 0;
        }

        var counted = Math.Clamp(amount, -row.Amount, payout.DailyCap - row.Amount);

        if (counted <= 0 && amount > 0)
            return 0;

        row.Amount += counted;
        await dbCtx.SaveChangesAsync(ct);

        return counted;
    }
}
