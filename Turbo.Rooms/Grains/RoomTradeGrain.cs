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
using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Trading.Enums;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains;

/// <summary>
/// Trades between two players in one room. Offers are edited while open; once both accept,
/// both confirm again and the items change hands through the inventories, all of one side or
/// none. A party leaving the room ends the trade.
///
/// Nothing here is persisted: a trade is a conversation, and the items only move in the
/// inventories' own write-through commit. Deactivation therefore closes whatever is still open
/// instead of flushing anything.
///
/// This grain calls the room grain (who is the partner, show the trading status) and awaits
/// it. The room grain must never await this grain back; it fires
/// <see cref="CloseForPlayerAsync"/> and moves on.
/// </summary>
internal sealed class RoomTradeGrain : Grain, IRoomTradeGrain
{
    /// <summary>Credits cannot be offered by this client, so both sides always carry none.</summary>
    private const int NO_CREDITS = 0;

    private readonly RoomConfig _roomConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IRoomTradeGrain> _logger;

    private readonly RoomTradeLiveState _state;

    private RoomId RoomId => _state.RoomId;

    public RoomTradeGrain(
        IOptions<RoomConfig> roomConfig,
        IGrainFactory grainFactory,
        ILogger<IRoomTradeGrain> logger
    )
    {
        _roomConfig = roomConfig.Value;
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

        if (!parties.InitiatorMayTrade || !await HasTradePerkAsync(parties.InitiatorId, ct))
        {
            await _grainFactory.SendComposerToPlayerAsync(
                parties.InitiatorId,
                new TradingYouAreNotAllowedEventMessageComposer(),
                ct
            );

            return false;
        }

        if (!parties.PartnerMayTrade || !await HasTradePerkAsync(parties.PartnerId, ct))
        {
            await _grainFactory.SendComposerToPlayerAsync(
                parties.InitiatorId,
                new TradingOtherNotAllowedEventMessageComposer(),
                ct
            );

            return false;
        }

        var session = new TradeSession
        {
            InitiatorId = parties.InitiatorId,
            PartnerId = parties.PartnerId,
        };

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
                PlayerCanTrade = true,
                OtherPlayerId = session.PartnerId,
                OtherPlayerCanTrade = true,
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
            session.State = TradeStateType.Open;

            await ResetAcceptsAsync(session, ct);

            return true;
        }

        session.SideOf(ctx.PlayerId).Confirmed = true;

        if (!session.BothConfirmed)
            return true;

        await CommitAsync(session, ct);

        return true;
    }

    private async Task CommitAsync(TradeSession session, CancellationToken ct)
    {
        session.State = TradeStateType.Completed;

        var initiatorItems = session.InitiatorSide.Items.Keys.ToImmutableArray();
        var partnerItems = session.PartnerSide.Items.Keys.ToImmutableArray();

        var toPartner = await _grainFactory
            .GetInventoryGrain(session.InitiatorId)
            .TransferFurnitureAsync(initiatorItems, session.PartnerId, ct);

        if (!toPartner)
        {
            await EndAsync(session, session.InitiatorId, TradeCloseReasonType.CommitError, ct);

            return;
        }

        var toInitiator = await _grainFactory
            .GetInventoryGrain(session.PartnerId)
            .TransferFurnitureAsync(partnerItems, session.InitiatorId, ct);

        if (!toInitiator)
        {
            // The first half already moved; hand it back rather than leave a one-sided trade.
            var returned = await _grainFactory
                .GetInventoryGrain(session.PartnerId)
                .TransferFurnitureAsync(initiatorItems, session.InitiatorId, ct);

            if (!returned)
                _logger.LogError(
                    "Trade in room {RoomId} between {InitiatorId} and {PartnerId} failed half way and the first half could not be returned",
                    RoomId,
                    session.InitiatorId,
                    session.PartnerId
                );

            await EndAsync(session, session.PartnerId, TradeCloseReasonType.CommitError, ct);

            return;
        }

        await SendToBothAsync(session, new TradingCompletedEventMessageComposer(), ct);
        await ForgetAsync(session, ct);
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

    /// <summary>The account's TRADE perk, when the hotel requires it; the room mode is the room's call.</summary>
    private async Task<bool> HasTradePerkAsync(PlayerId playerId, CancellationToken ct)
    {
        if (!_roomConfig.TradeRequiresPerk)
            return true;

        var summary = await _grainFactory.GetPlayerGrain(playerId).GetSummaryAsync(ct);

        return summary.Perks.HasFlag(PlayerPerkFlags.Trade);
    }

    private bool TryGetOpenSession(PlayerId playerId, out TradeSession session) =>
        _state.TradesByPlayerId.TryGetValue(playerId, out session!)
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
