using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Trading;
using Turbo.Primitives.Trading.Enums;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains;

/// <summary>
/// Trades between two players in one room. Offers are edited while open; once both accept,
/// both confirm again and both offers change hands in one custody transaction through the
/// initiating inventory. A party leaving the room ends the conversation.
///
/// Open conversations are not persisted. A submitted exchange has a durable receipt and
/// inventory-repair obligations; deactivation closes the conversation without reversing it.
///
/// This grain calls the room grain (who is the partner, show the trading status) and awaits
/// it. The room grain must never await this grain back; it fires
/// <see cref="CloseForPlayerAsync"/> and moves on.
/// </summary>
internal sealed class RoomTradeGrain : Grain, IRoomTradeGrain
{
    /// <summary>Credits cannot be offered by this client, so both sides always carry none.</summary>
    private const int NO_CREDITS = 0;

    /// <summary>The notification Habbo sends a player who starts a trade without the trading pass.</summary>
    private const string TRADING_PERK = "trade.trading_perk";

    private readonly RoomConfig _roomConfig;
    private readonly IPlayerTradePolicy _tradePolicy;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IRoomTradeGrain> _logger;

    private readonly RoomTradeLiveState _state;

    private RoomId RoomId => _state.RoomId;

    public RoomTradeGrain(
        IOptions<RoomConfig> roomConfig,
        IPlayerTradePolicy tradePolicy,
        IGrainFactory grainFactory,
        ILogger<IRoomTradeGrain> logger
    )
    {
        _roomConfig = roomConfig.Value;
        _tradePolicy = tradePolicy;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { RoomId = this.GetRoomId() };
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        foreach (var session in _state.TradesByPlayerId.Values.Distinct().ToList())
        {
            try
            {
                await EndAsync(session, session.InitiatorId, TradeCloseReasonType.Closed, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not close the trade between {InitiatorId} and {PartnerId} while the trade grain of room {RoomId} deactivated",
                    session.InitiatorId,
                    session.PartnerId,
                    RoomId
                );
            }
        }
    }

    public Task<bool> OpenAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    ) => RunAsync(ctx, "open", () => OpenCoreAsync(ctx, targetObjectId, ct));

    public Task<bool> AddItemsAsync(
        ActionContext ctx,
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    ) => RunAsync(ctx, "offer items in", () => AddItemsCoreAsync(ctx, itemIds, ct));

    public Task<bool> RemoveItemAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    ) => RunAsync(ctx, "withdraw an item from", () => RemoveItemCoreAsync(ctx, itemId, ct));

    public Task<bool> AcceptAsync(ActionContext ctx, bool accept, CancellationToken ct) =>
        RunAsync(ctx, accept ? "accept" : "unaccept", () => AcceptCoreAsync(ctx, accept, ct));

    public Task<bool> ConfirmAsync(ActionContext ctx, bool accept, CancellationToken ct) =>
        RunAsync(ctx, accept ? "confirm" : "decline", () => ConfirmCoreAsync(ctx, accept, ct));

    public Task<bool> CloseAsync(ActionContext ctx, CancellationToken ct) =>
        RunAsync(
            ctx,
            "close",
            async () =>
            {
                if (!_state.TradesByPlayerId.TryGetValue(ctx.PlayerId, out var session))
                    return false;

                await EndAsync(session, ctx.PlayerId, TradeCloseReasonType.Closed, ct);

                return true;
            }
        );

    public async Task CloseForPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        if (!_state.TradesByPlayerId.TryGetValue(playerId, out var session))
            return;

        await EndAsync(session, playerId, TradeCloseReasonType.Closed, ct);
    }

    private async Task<bool> OpenCoreAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    )
    {
        var parties = await _grainFactory
            .GetRoomGrain(RoomId)
            .GetTradePartiesAsync(ctx, targetObjectId, ct);

        if (parties is null)
            return false;

        if (_state.TradesByPlayerId.ContainsKey(parties.InitiatorId))
        {
            await SendOpenFailedAsync(
                parties.InitiatorId,
                TradeOpenFailedType.YouAreAlreadyTrading,
                parties.PartnerName,
                ct
            );

            return false;
        }

        if (_state.TradesByPlayerId.ContainsKey(parties.PartnerId))
        {
            await SendOpenFailedAsync(
                parties.InitiatorId,
                TradeOpenFailedType.OtherAlreadyTrading,
                parties.PartnerName,
                ct
            );

            return false;
        }

        if (!parties.InitiatorMayTrade || !parties.PartnerMayTrade)
        {
            await SendOpenFailedAsync(
                parties.InitiatorId,
                TradeOpenFailedType.RoomDisallows,
                parties.PartnerName,
                ct
            );

            return false;
        }

        var initiator = await _tradePolicy.GetTradeStandingAsync(parties.InitiatorId, ct);

        if (initiator == TradeStandingType.Locked)
        {
            await SendOpenFailedAsync(
                parties.InitiatorId,
                TradeOpenFailedType.YouAreTradeLocked,
                parties.PartnerName,
                ct
            );

            return false;
        }

        // Habbo answers a player without the pass with a notification of its own, and the
        // partner hears nothing (official client capture, 2026-10-17 and 2026-10-22).
        if (initiator == TradeStandingType.NoPass)
        {
            await _grainFactory.SendComposerToPlayerAsync(
                parties.InitiatorId,
                new NotificationDialogMessageComposer { NotificationType = TRADING_PERK },
                ct
            );

            return false;
        }

        var partner = await _tradePolicy.GetTradeStandingAsync(parties.PartnerId, ct);

        if (partner is TradeStandingType.Locked or TradeStandingType.NoPass)
        {
            await SendOpenFailedAsync(
                parties.InitiatorId,
                TradeOpenFailedType.OtherCannotTrade,
                parties.PartnerName,
                ct
            );

            return false;
        }

        var session = new TradeSession
        {
            InitiatorId = parties.InitiatorId,
            PartnerId = parties.PartnerId,
        };

        session.InitiatorSide.CanOffer = initiator == TradeStandingType.Full;
        session.PartnerSide.CanOffer = partner == TradeStandingType.Full;

        _state.TradesByPlayerId[session.InitiatorId] = session;
        _state.TradesByPlayerId[session.PartnerId] = session;

        await _grainFactory
            .GetRoomGrain(RoomId)
            .SetTradingStatusAsync([session.InitiatorId, session.PartnerId], true, ct);

        await SendToBothAsync(
            session,
            new TradingOpenEventMessageComposer
            {
                PlayerId = session.InitiatorId,
                PlayerCanTrade = session.InitiatorSide.CanOffer,
                OtherPlayerId = session.PartnerId,
                OtherPlayerCanTrade = session.PartnerSide.CanOffer,
            },
            ct
        );

        return true;
    }

    private async Task<bool> AddItemsCoreAsync(
        ActionContext ctx,
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    )
    {
        if (!TryGetOpenSession(ctx.PlayerId, out var session))
        {
            await _grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new TradingNotOpenEventMessageComposer(),
                ct
            );

            return false;
        }

        var side = session.SideOf(ctx.PlayerId);

        // A player without the pass, where the hotel lets them receive only: the client hides
        // their offer button, and this warning is what the trade window shows if one gets here.
        if (!side.CanOffer)
        {
            await _grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new TradingYouAreNotAllowedEventMessageComposer(),
                ct
            );

            return false;
        }

        var wanted = itemIds.Where(id => !side.Items.ContainsKey(id)).Distinct().ToImmutableArray();

        if (wanted.Length == 0)
            return false;

        if (side.Items.Count + wanted.Length > _roomConfig.TradeMaxItemsPerSide)
        {
            _logger.LogDebug(
                "Player {PlayerId} offered more than {Max} items in a trade in room {RoomId}",
                ctx.PlayerId,
                _roomConfig.TradeMaxItemsPerSide,
                RoomId
            );

            return false;
        }

        var items = await _grainFactory
            .GetInventoryGrain(ctx.PlayerId)
            .GetItemSnapshotsAsync(wanted, ct);
        var added = false;

        foreach (var item in items)
        {
            if (!item.Definition.CanTrade || item.RoomId > 0)
                continue;

            side.Items[item.ItemId] = item;
            added = true;
        }

        if (!added)
            return false;

        await ResetAcceptsAsync(session, ct);
        await SendItemListAsync(session, ct);

        return true;
    }

    private async Task<bool> RemoveItemCoreAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        if (!TryGetOpenSession(ctx.PlayerId, out var session))
        {
            await _grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new TradingNotOpenEventMessageComposer(),
                ct
            );

            return false;
        }

        if (!session.SideOf(ctx.PlayerId).Items.Remove(itemId))
            return false;

        await ResetAcceptsAsync(session, ct);
        await SendItemListAsync(session, ct);

        return true;
    }

    /// <summary>The second acceptance moves the trade to confirmation.</summary>
    private async Task<bool> AcceptCoreAsync(ActionContext ctx, bool accept, CancellationToken ct)
    {
        if (!TryGetOpenSession(ctx.PlayerId, out var session))
        {
            await _grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new TradingNotOpenEventMessageComposer(),
                ct
            );

            return false;
        }

        session.SideOf(ctx.PlayerId).Accepted = accept;

        await SendToBothAsync(
            session,
            new TradingAcceptEventMessageComposer { PlayerId = ctx.PlayerId, Accepted = accept },
            ct
        );

        if (!session.BothAccepted)
            return true;

        session.State = TradeStateType.Confirming;

        await SendToBothAsync(session, new TradingConfirmationEventMessageComposer(), ct);

        return true;
    }

    /// <summary>A decline drops both acceptances and reopens the offers.</summary>
    private async Task<bool> ConfirmCoreAsync(ActionContext ctx, bool accept, CancellationToken ct)
    {
        if (
            !_state.TradesByPlayerId.TryGetValue(ctx.PlayerId, out var session)
            || session.State != TradeStateType.Confirming
        )
        {
            await _grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new TradingNotOpenEventMessageComposer(),
                ct
            );

            return false;
        }

        if (!accept)
        {
            // Once submitted, an outcome may be unknown. A decline must not replace the
            // frozen intent; another confirmation retries its original operation identity.
            if (session.CommitCause is not null)
                return false;
            session.State = TradeStateType.Open;

            await ResetAcceptsAsync(session, ct);

            return true;
        }

        session.SideOf(ctx.PlayerId).Confirmed = true;

        if (!session.BothConfirmed)
            return true;

        await CommitAsync(session, ctx.PlayerId, ct);

        return true;
    }

    /// <summary>
    /// Both halves of the trade, as one operation confirmed by <paramref name="confirmedBy"/> and
    /// caused by this room. Both offers, their receipt and movements commit together. A lost
    /// response retains the frozen request for retry; delivery never reverses a committed exchange.
    /// </summary>
    private async Task CommitAsync(TradeSession session, PlayerId confirmedBy, CancellationToken ct)
    {
        session.CommitRequest ??= new FurnitureTradeRequest
        {
            InitiatorId = session.InitiatorId,
            PartnerId = session.PartnerId,
            InitiatorItems = [.. session.InitiatorSide.Items.Values],
            PartnerItems = [.. session.PartnerSide.Items.Values],
        };
        session.CommitCause ??= MovementCause.New(
            MovementCauses.TRADE,
            confirmedBy.Value,
            RoomId.Value
        );
        var committed = await _grainFactory
            .GetInventoryGrain(session.InitiatorId)
            .CommitFurnitureTradeAsync(session.CommitRequest, session.CommitCause, ct);

        if (!committed)
        {
            await EndAsync(session, session.InitiatorId, TradeCloseReasonType.CommitError, ct);

            return;
        }

        session.State = TradeStateType.Completed;
        await Task.WhenAll(
            ReconcileCommittedAsync(session.InitiatorId, ct),
            ReconcileCommittedAsync(session.PartnerId, ct)
        );
        // Remove the conversation even if clearing the room's trading badge or telling a
        // disconnected player fails. The durable receipt remains the completion authority.
        try
        {
            await ForgetAsync(session, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to clear completed trade status in room {RoomId}",
                RoomId
            );
        }
        try
        {
            await SendToBothAsync(session, new TradingCompletedEventMessageComposer(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to notify players of committed trade {OperationId}",
                session.CommitCause.OperationId
            );
        }
    }

    private async Task ReconcileCommittedAsync(PlayerId player, CancellationToken ct)
    {
        try
        {
            await _grainFactory.GetInventoryGrain(player).ReconcileFurnitureTradesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to reconcile committed trade inventory for player {PlayerId}; durable recovery will retry",
                player
            );
        }
    }

    private async Task EndAsync(
        TradeSession session,
        PlayerId closerId,
        TradeCloseReasonType reason,
        CancellationToken ct
    )
    {
        await ForgetAsync(session, ct);

        await SendToBothAsync(
            session,
            new TradingCloseEventMessageComposer { PlayerId = closerId, Reason = reason },
            ct
        );
    }

    private Task ForgetAsync(TradeSession session, CancellationToken ct)
    {
        _state.TradesByPlayerId.Remove(session.InitiatorId);
        _state.TradesByPlayerId.Remove(session.PartnerId);

        return _grainFactory
            .GetRoomGrain(RoomId)
            .SetTradingStatusAsync([session.InitiatorId, session.PartnerId], false, ct);
    }

    private async Task ResetAcceptsAsync(TradeSession session, CancellationToken ct)
    {
        foreach (var playerId in new[] { session.InitiatorId, session.PartnerId })
        {
            var side = session.SideOf(playerId);

            side.Confirmed = false;

            if (!side.Accepted)
                continue;

            side.Accepted = false;

            await SendToBothAsync(
                session,
                new TradingAcceptEventMessageComposer { PlayerId = playerId, Accepted = false },
                ct
            );
        }
    }

    private Task SendItemListAsync(TradeSession session, CancellationToken ct) =>
        SendToBothAsync(
            session,
            new TradingItemListEventMessageComposer
            {
                FirstPlayerId = session.InitiatorId,
                FirstItems = [.. session.InitiatorSide.Items.Values],
                FirstCredits = NO_CREDITS,
                SecondPlayerId = session.PartnerId,
                SecondItems = [.. session.PartnerSide.Items.Values],
                SecondCredits = NO_CREDITS,
            },
            ct
        );

    private bool TryGetOpenSession(
        PlayerId playerId,
        [NotNullWhen(true)] out TradeSession? session
    ) =>
        _state.TradesByPlayerId.TryGetValue(playerId, out session)
        && session.State == TradeStateType.Open;

    private async Task<bool> RunAsync(ActionContext ctx, string action, Func<Task<bool>> body)
    {
        try
        {
            return await body();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Player {PlayerId} failed to {Action} a trade in room {RoomId}",
                ctx.PlayerId,
                action,
                RoomId
            );

            return false;
        }
        finally
        {
            KeepWhileTrading();
        }
    }

    /// <summary>
    /// Nothing else calls this grain while two players look at their offers, and Orleans collects
    /// an idle activation while its players stay online, which would close every open trade. An
    /// open trade keeps it loaded for <see cref="RoomConfig.TradeKeepAliveMinutes"/> past each
    /// action instead.
    /// </summary>
    private void KeepWhileTrading()
    {
        if (_state.TradesByPlayerId.Count > 0)
            DelayDeactivation(TimeSpan.FromMinutes(_roomConfig.TradeKeepAliveMinutes));
    }

    private Task SendOpenFailedAsync(
        PlayerId playerId,
        TradeOpenFailedType reason,
        string otherName,
        CancellationToken ct
    ) =>
        _grainFactory.SendComposerToPlayerAsync(
            playerId,
            new TradeOpenFailedEventPaserMessageComposer
            {
                Reason = reason,
                OtherPlayerName = otherName,
            },
            ct
        );

    private Task SendToBothAsync(TradeSession session, IComposer composer, CancellationToken ct) =>
        _grainFactory.SendComposerToPlayersAsync(
            [session.InitiatorId, session.PartnerId],
            composer,
            ct
        );
}
