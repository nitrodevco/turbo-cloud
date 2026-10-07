using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.WiredTrading;

/// <summary>
/// A player's trade with wired: the inventory window in which they offer items, then accept,
/// then confirm. Either a deposit into a chest, or a contract's payment or trade, which is
/// held to the contract's requirements, may time out, and may be cancelled by wired.
/// <para>
/// Nothing is persisted and nothing is reserved; the offer is snapshots, and the room checks
/// and carries out the trade when it is confirmed. Deactivation simply forgets an open trade,
/// whose window the client closes on its own.
/// </para>
/// <para>
/// Awaits the player's inventory and the room. The room only tells this grain, so the two
/// never wait on each other.
/// </para>
/// </summary>
internal sealed class WiredTradeGrain : Grain, IWiredTradeGrain
{
    /// <summary>The layout the trade window draws a plain deposit with.</summary>
    private const string GENERIC_LAYOUT = "generic";

    private readonly WiredChestConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IWiredTradeGrain> _logger;

    private readonly WiredTradeLiveState _state;

    private IDisposable? _timeoutTimer;
    private IDisposable? _cancelTimer;

    public WiredTradeGrain(
        IOptions<WiredChestConfig> config,
        IGrainFactory grainFactory,
        ILogger<IWiredTradeGrain> logger
    )
    {
        _config = config.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        StopTimers();

        return Task.CompletedTask;
    }

    public async Task StartChestDepositAsync(
        RoomId roomId,
        RoomObjectId chestId,
        WiredChestKind kind,
        CancellationToken ct
    )
    {
        var replacing = await EndForReplacementAsync(ct);

        _state.Session = new()
        {
            RoomId = roomId,
            TargetId = chestId,
            Kind = kind,
        };

        await SendAsync(
            [
                new WiredTradeInitiateMessageComposer
                {
                    Requirement = new()
                    {
                        Type =
                            kind == WiredChestKind.Coins
                                ? TradeRequirementType.AnyCoins
                                : TradeRequirementType.AnyFurni,
                        YouGetText = string.Empty,
                        LayoutType = GENERIC_LAYOUT,
                        Rules = null,
                    },
                    ShowRequirementsImmediate = false,
                    OverridePreviousTrade = replacing,
                    TimeoutSeconds = 0,
                },
                BuildItemsUpdate(_state.Session),
            ],
            ct
        );
    }

    public async Task StartContractAsync(WiredContractTradeRequest request, CancellationToken ct)
    {
        // One trade at a time; only one wired just cancelled gives way to the next.
        if (_state.Session is { IsCancelPending: false })
        {
            await _grainFactory
                .GetRoomGrain(request.RoomId)
                .ReportWiredTransactionFailedAsync(
                    _state.PlayerId,
                    request.SourceId,
                    WiredTransactionFailureType.AlreadyTrading,
                    ct
                );
            await SendAsync(
                [
                    new WiredTransactionFailMessageComposer
                    {
                        FailureType = WiredTransactionFailureType.AlreadyTrading,
                    },
                ],
                ct
            );

            return;
        }

        var replacing = await EndForReplacementAsync(ct);

        _state.Session = new()
        {
            RoomId = request.RoomId,
            TargetId = request.SourceId,
            Contract = request,
        };

        if (request.TimeoutSeconds > 0)
            _timeoutTimer = this.RegisterGrainTimer<object?>(
                static async (self, ct) => await ((WiredTradeGrain)self!).TimeOutAsync(ct),
                this,
                TimeSpan.FromSeconds(request.TimeoutSeconds),
                Timeout.InfiniteTimeSpan
            );

        await SendAsync(
            [
                new WiredTradeInitiateMessageComposer
                {
                    Requirement = BuildRequirement(request),
                    ShowRequirementsImmediate = true,
                    OverridePreviousTrade = replacing,
                    TimeoutSeconds = request.TimeoutSeconds,
                },
                BuildItemsUpdate(_state.Session),
            ],
            ct
        );
    }

    public Task CancelByWiredAsync(ImmutableArray<RoomObjectId> sourceIds, CancellationToken ct)
    {
        if (
            _state.Session is not { Contract: { } request } session
            || session.IsCancelPending
            || (!sourceIds.IsDefaultOrEmpty && !sourceIds.Contains(request.SourceId))
        )
            return Task.CompletedTask;

        session.IsCancelPending = true;
        _cancelTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((WiredTradeGrain)self!).FinishCancelAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_config.CancelGraceMs),
            Timeout.InfiniteTimeSpan
        );

        return Task.CompletedTask;
    }

    public async Task AddItemsAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct)
    {
        if (_state.Session is not { } session || itemIds.IsDefaultOrEmpty)
            return;

        var offered = session.Offer.Select(x => x.ItemId).ToHashSet();
        var wanted = itemIds.Where(id => !offered.Contains(id)).Distinct().ToImmutableArray();

        if (wanted.IsEmpty)
            return;

        var items = await _grainFactory
            .GetInventoryGrain(_state.PlayerId)
            .GetItemSnapshotsAsync(wanted, ct);

        // The trade may have ended or changed while the inventory answered.
        if (!ReferenceEquals(_state.Session, session))
            return;

        WiredTradeErrorType? error =
            items.Length == wanted.Length ? null : WiredTradeErrorType.InvalidItem;

        foreach (var item in items)
        {
            if (!Fits(session, item))
            {
                error = WiredTradeErrorType.InvalidItem;

                continue;
            }

            if (session.Offer.Count >= _config.MaxItemsPerDeposit)
            {
                error = WiredTradeErrorType.TooManyItems;

                break;
            }

            session.Offer.Add(item);
        }

        session.IsAccepted = false;

        List<IComposer> composers = [BuildItemsUpdate(session)];

        if (error is { } refused)
            composers.Add(new WiredTradeTransactionNotificationMessageComposer { Error = refused });

        await SendAsync(composers, ct);
    }

    public async Task RemoveItemsAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct)
    {
        if (_state.Session is not { } session || itemIds.IsDefaultOrEmpty)
            return;

        var removed = itemIds.ToHashSet();

        session.Offer.RemoveAll(x => removed.Contains(x.ItemId));
        session.IsAccepted = false;

        await SendAsync([BuildItemsUpdate(session)], ct);
    }

    public async Task ConfirmAsync(bool isFinalConfirm, CancellationToken ct)
    {
        if (_state.Session is not { } session || !CanAccept(session))
            return;

        if (!isFinalConfirm)
        {
            session.IsAccepted = true;

            return;
        }

        if (!session.IsAccepted)
        {
            _logger.LogWarning(
                "Player {PlayerId} confirmed a wired trade they had not accepted; ignored",
                _state.PlayerId
            );

            return;
        }

        // The trade ends here whatever the room says; a second confirm finds nothing.
        End();

        var (failure, success) = await CarryOutAsync(session, ct);

        if (failure is { } refused)
        {
            await SendAsync([new WiredTradeCancelledMessageComposer { FailureType = refused }], ct);

            return;
        }

        await SendAsync(
            [
                new WiredTradeCompletedMessageComposer(),
                new WiredTransactionSuccessMessageComposer
                {
                    Contents = new()
                    {
                        Type = success,
                        RewardContents = null,
                        RewardText = null,
                        OpenByDefault = false,
                    },
                },
            ],
            ct
        );
    }

    public Task CancelAsync(WiredTransactionFailureType reason, CancellationToken ct) =>
        _state.Session is { } session ? EndAsync(session, reason, ct) : Task.CompletedTask;

    /// <summary>The room carries the trade out; what to tell the player on success.</summary>
    private async Task<(
        WiredTransactionFailureType? Failure,
        WiredTransactionSuccessType Success
    )> CarryOutAsync(WiredTradeSession session, CancellationToken ct)
    {
        var room = _grainFactory.GetRoomGrain(session.RoomId);
        var ctx = ActionContext.CreateForPlayer(_state.PlayerId, session.RoomId);

        try
        {
            if (session.Contract is not { } request)
            {
                var deposit = await room.DepositIntoWiredChestAsync(
                    ctx,
                    session.TargetId,
                    [.. session.Offer.Select(x => x.ItemId)],
                    ct
                );

                return (deposit.Failure, WiredTransactionSuccessType.Deposit);
            }

            var (ruleIndex, timesMet) = WiredContractOffers.Evaluate(request, session.Offer);
            var times = WiredContractOffers.TimesToCarryOut(request, timesMet);
            var failure = await room.CompleteWiredContractTradeAsync(
                ctx,
                request,
                WiredContractOffers.Take(request, session.Offer, ruleIndex, times),
                times,
                ct
            );

            return (
                failure,
                request.Contract.Type == WiredContractType.Trade
                    ? WiredTransactionSuccessType.Trade
                    : WiredTransactionSuccessType.Payment
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Wired trade of {Count} items by player {PlayerId} with {TargetId} in room {RoomId} failed",
                session.Offer.Count,
                _state.PlayerId,
                session.TargetId,
                session.RoomId
            );

            return (WiredTransactionFailureType.InternalError, WiredTransactionSuccessType.Deposit);
        }
    }

    private Task TimeOutAsync(CancellationToken ct) =>
        _state.Session is { Contract: not null } session
            ? EndAsync(session, WiredTransactionFailureType.Timeout, ct)
            : Task.CompletedTask;

    private Task FinishCancelAsync(CancellationToken ct) =>
        _state.Session is { IsCancelPending: true } session
            ? EndAsync(session, WiredTransactionFailureType.TradeCancelled, ct)
            : Task.CompletedTask;

    /// <summary>Ends a trade that did not go through: the window closes and the triggers hear why.</summary>
    private async Task EndAsync(
        WiredTradeSession session,
        WiredTransactionFailureType reason,
        CancellationToken ct
    )
    {
        End();

        await SendAsync([new WiredTradeCancelledMessageComposer { FailureType = reason }], ct);

        if (session.Contract is { } request)
            await _grainFactory
                .GetRoomGrain(session.RoomId)
                .ReportWiredTransactionFailedAsync(_state.PlayerId, request.SourceId, reason, ct);
    }

    /// <summary>
    /// Makes way for a new trade. A contract trade wired cancelled is replaced quietly: the
    /// window stays, and the cancellation is still reported. True when a trade was replaced,
    /// which the client then does without closing its window.
    /// </summary>
    private async Task<bool> EndForReplacementAsync(CancellationToken ct)
    {
        if (_state.Session is not { } session)
            return false;

        End();

        if (session is { IsCancelPending: true, Contract: { } request })
            await _grainFactory
                .GetRoomGrain(session.RoomId)
                .ReportWiredTransactionFailedAsync(
                    _state.PlayerId,
                    request.SourceId,
                    WiredTransactionFailureType.TradeCancelled,
                    ct
                );

        return true;
    }

    private void End()
    {
        _state.Session = null;

        StopTimers();
    }

    private void StopTimers()
    {
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
        _cancelTimer?.Dispose();
        _cancelTimer = null;
    }

    /// <summary>
    /// What may be offered. A deposit takes credit furni into a credit chest and other tradeable
    /// furni into a furni chest; a contract takes what its requirements ask for.
    /// </summary>
    private static bool Fits(WiredTradeSession session, FurnitureItemSnapshot item) =>
        session.Contract is { } request
            ? WiredContractOffers.CanOffer(request, item)
            : item.Definition.CanTrade
                && item.RoomId <= 0
                && CreditFurniValue.TryParse(item.Definition.Name, out _)
                    == (session.Kind == WiredChestKind.Coins);

    private static bool CanAccept(WiredTradeSession session) =>
        session.Contract is { } request
            ? WiredContractOffers.CanAccept(
                request,
                WiredContractOffers.Evaluate(request, session.Offer).Times
            )
            : session.Offer.Count > 0;

    private static TradeRequirementSnapshot BuildRequirement(WiredContractTradeRequest request) =>
        new()
        {
            Type = request.IsDonation ? TradeRequirementType.Anything : TradeRequirementType.Rules,
            YouGetText = request.Contract.ReceiveText,
            LayoutType =
                request.Contract.LayoutType.Length > 0
                    ? request.Contract.LayoutType
                    : GENERIC_LAYOUT,
            Rules = request.IsDonation
                ? null
                : new()
                {
                    Definition = request.Contract.Definition,
                    Type = request.RulesType,
                    Multiplier = request.Multiplier,
                    AutoMultiplierMax = request.Multiplier,
                },
        };

    /// <summary>
    /// The window's two sides: the player's offer with the credits their credit furni are worth,
    /// and what wired gives back. The client reads <c>Extra</c> as how many times the
    /// requirements are met.
    /// </summary>
    private WiredTradeItemsUpdateMessageComposer BuildItemsUpdate(WiredTradeSession session)
    {
        var timesMet =
            session.Contract is { } request
                ? Math.Min(
                    WiredContractOffers.Evaluate(request, session.Offer).Times,
                    request.Multiplier
                )
            : session.Offer.Count > 0 ? 1
            : 0;
        var reward = session.Contract is { Contract.Type: WiredContractType.Trade } trade
            ? WiredContractOffers.Reward(
                trade.Contract,
                Math.Max(1, WiredContractOffers.TimesToCarryOut(trade, timesMet))
            )
            : null;

        return new()
        {
            FirstPlayerId = _state.PlayerId,
            FirstItems = [.. session.Offer],
            FirstCredits = session.Contract is null
                ? 0
                : WiredContractOffers.Credits(session.Offer),
            SecondPlayerId = PlayerId.Parse(0),
            SecondItems = [],
            SecondCredits =
                reward?.Nodes.Where(x => x.Type == TradeRequirementNodeType.Coin).Sum(x => x.Amount)
                ?? 0,
            CanAccept = CanAccept(session),
            Extra = timesMet,
        };
    }

    private Task SendAsync(IReadOnlyList<IComposer> composers, CancellationToken ct) =>
        _grainFactory.GetPlayerPresenceGrain(_state.PlayerId).SendComposerAsync(composers, ct);
}
