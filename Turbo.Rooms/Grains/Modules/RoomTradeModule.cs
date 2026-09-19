using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Trading.Enums;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Trades between two players in the room. Offers are edited while open; once both accept,
/// both confirm again and the items change hands through the inventories, all of one side or
/// none. A party leaving the room ends the trade.
/// </summary>
public sealed class RoomTradeModule(RoomGrain roomGrain)
{
    /// <summary>Credits cannot be offered by this client, so both sides always carry none.</summary>
    private const int NO_CREDITS = 0;

    private readonly RoomGrain _roomGrain = roomGrain;

    public async Task<bool> OpenAsync(
        ActionContext ctx,
        RoomObjectId targetObjectId,
        CancellationToken ct
    )
    {
        if (!TryGetPlayer(ctx.PlayerId, out var initiator))
            return false;

        if (
            !_roomGrain._state.AvatarsByObjectId.TryGetValue(targetObjectId, out var avatar)
            || avatar is not IRoomPlayer partner
            || partner.PlayerId == ctx.PlayerId
        )
            return false;

        if (_roomGrain._state.TradesByPlayerId.ContainsKey(ctx.PlayerId))
        {
            await SendOpenFailedAsync(
                ctx.PlayerId,
                TradeOpenFailedType.YouAreAlreadyTrading,
                partner.Name,
                ct
            );

            return false;
        }

        if (_roomGrain._state.TradesByPlayerId.ContainsKey(partner.PlayerId))
        {
            await SendOpenFailedAsync(
                ctx.PlayerId,
                TradeOpenFailedType.OtherAlreadyTrading,
                partner.Name,
                ct
            );

            return false;
        }

        if (!await CanTradeInRoomAsync(ctx.PlayerId, ct))
        {
            await SendToPlayerAsync(
                ctx.PlayerId,
                new TradingYouAreNotAllowedEventMessageComposer(),
                ct
            );

            return false;
        }

        if (!await CanTradeInRoomAsync(partner.PlayerId, ct))
        {
            await SendToPlayerAsync(
                ctx.PlayerId,
                new TradingOtherNotAllowedEventMessageComposer(),
                ct
            );

            return false;
        }

        var session = new TradeSession { InitiatorId = ctx.PlayerId, PartnerId = partner.PlayerId };

        _roomGrain._state.TradesByPlayerId[ctx.PlayerId] = session;
        _roomGrain._state.TradesByPlayerId[partner.PlayerId] = session;

        initiator.AddStatus(AvatarStatusType.Trading, string.Empty);
        partner.AddStatus(AvatarStatusType.Trading, string.Empty);

        var open = new TradingOpenEventMessageComposer
        {
            PlayerId = ctx.PlayerId,
            PlayerCanTrade = true,
            OtherPlayerId = partner.PlayerId,
            OtherPlayerCanTrade = true,
        };

        await SendToBothAsync(session, open, ct);

        return true;
    }

    public async Task<bool> AddItemsAsync(
        ActionContext ctx,
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    )
    {
        if (!TryGetOpenSession(ctx.PlayerId, out var session))
        {
            await SendToPlayerAsync(ctx.PlayerId, new TradingNotOpenEventMessageComposer(), ct);

            return false;
        }

        var side = session.SideOf(ctx.PlayerId);
        var wanted = itemIds.Where(id => !side.Items.ContainsKey(id)).Distinct().ToImmutableArray();

        if (wanted.Length == 0)
            return false;

        if (side.Items.Count + wanted.Length > _roomGrain._roomConfig.TradeMaxItemsPerSide)
        {
            _roomGrain._logger.LogDebug(
                "Player {PlayerId} offered more than {Max} items in a trade in room {RoomId}",
                ctx.PlayerId,
                _roomGrain._roomConfig.TradeMaxItemsPerSide,
                _roomGrain.RoomId
            );

            return false;
        }

        var items = await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
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

    public async Task<bool> RemoveItemAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        if (!TryGetOpenSession(ctx.PlayerId, out var session))
        {
            await SendToPlayerAsync(ctx.PlayerId, new TradingNotOpenEventMessageComposer(), ct);

            return false;
        }

        if (!session.SideOf(ctx.PlayerId).Items.Remove(itemId))
            return false;

        await ResetAcceptsAsync(session, ct);
        await SendItemListAsync(session, ct);

        return true;
    }

    /// <summary>Accept or withdraw acceptance of the offers; the second acceptance moves to confirmation.</summary>
    public async Task<bool> AcceptAsync(ActionContext ctx, bool accept, CancellationToken ct)
    {
        if (!TryGetOpenSession(ctx.PlayerId, out var session))
        {
            await SendToPlayerAsync(ctx.PlayerId, new TradingNotOpenEventMessageComposer(), ct);

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

    /// <summary>The final confirmation; a decline drops both acceptances and reopens the offers.</summary>
    public async Task<bool> ConfirmAsync(ActionContext ctx, bool accept, CancellationToken ct)
    {
        if (
            !_roomGrain._state.TradesByPlayerId.TryGetValue(ctx.PlayerId, out var session)
            || session.State != TradeStateType.Confirming
        )
        {
            await SendToPlayerAsync(ctx.PlayerId, new TradingNotOpenEventMessageComposer(), ct);

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

    public async Task<bool> CloseAsync(ActionContext ctx, CancellationToken ct)
    {
        if (!_roomGrain._state.TradesByPlayerId.TryGetValue(ctx.PlayerId, out var session))
            return false;

        await EndAsync(session, ctx.PlayerId, TradeCloseReasonType.Closed, ct);

        return true;
    }

    /// <summary>A party is leaving the room; whatever trade they had ends for both.</summary>
    internal async Task CloseForPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        if (!_roomGrain._state.TradesByPlayerId.TryGetValue(playerId, out var session))
            return;

        await EndAsync(session, playerId, TradeCloseReasonType.Closed, ct);
    }

    private async Task CommitAsync(TradeSession session, CancellationToken ct)
    {
        session.State = TradeStateType.Completed;

        var initiatorItems = session.InitiatorSide.Items.Keys.ToImmutableArray();
        var partnerItems = session.PartnerSide.Items.Keys.ToImmutableArray();

        var toPartner = await _roomGrain
            ._grainFactory.GetInventoryGrain(session.InitiatorId)
            .TransferFurnitureAsync(initiatorItems, session.PartnerId, ct);

        if (!toPartner)
        {
            await EndAsync(session, session.InitiatorId, TradeCloseReasonType.CommitError, ct);

            return;
        }

        var toInitiator = await _roomGrain
            ._grainFactory.GetInventoryGrain(session.PartnerId)
            .TransferFurnitureAsync(partnerItems, session.InitiatorId, ct);

        if (!toInitiator)
        {
            // The first half already moved; hand it back rather than leave a one-sided trade.
            var returned = await _roomGrain
                ._grainFactory.GetInventoryGrain(session.PartnerId)
                .TransferFurnitureAsync(initiatorItems, session.InitiatorId, ct);

            if (!returned)
                _roomGrain._logger.LogError(
                    "Trade in room {RoomId} between {InitiatorId} and {PartnerId} failed half way and the first half could not be returned",
                    _roomGrain.RoomId,
                    session.InitiatorId,
                    session.PartnerId
                );

            await EndAsync(session, session.PartnerId, TradeCloseReasonType.CommitError, ct);

            return;
        }

        await SendToBothAsync(session, new TradingCompletedEventMessageComposer(), ct);

        Forget(session);
    }

    private async Task EndAsync(
        TradeSession session,
        PlayerId closerId,
        TradeCloseReasonType reason,
        CancellationToken ct
    )
    {
        Forget(session);

        await SendToBothAsync(
            session,
            new TradingCloseEventMessageComposer { PlayerId = closerId, Reason = reason },
            ct
        );
    }

    private void Forget(TradeSession session)
    {
        _roomGrain._state.TradesByPlayerId.Remove(session.InitiatorId);
        _roomGrain._state.TradesByPlayerId.Remove(session.PartnerId);

        foreach (var playerId in new[] { session.InitiatorId, session.PartnerId })
        {
            if (TryGetPlayer(playerId, out var player))
                player.RemoveStatus(AvatarStatusType.Trading);
        }
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

    /// <summary>Room trade mode first, then the account's TRADE perk when the hotel requires it.</summary>
    private async Task<bool> CanTradeInRoomAsync(PlayerId playerId, CancellationToken ct)
    {
        switch (_roomGrain._state.RoomSnapshot.TradeType)
        {
            case RoomTradeModeType.Disabled:
                return false;
            case RoomTradeModeType.RoomOwnerAndRights:
                if (
                    await _roomGrain.SecurityModule.GetControllerLevelAsync(playerId)
                    < RoomControllerType.Rights
                )
                    return false;
                break;
        }

        if (!_roomGrain._roomConfig.TradeRequiresPerk)
            return true;

        var summary = await _roomGrain._grainFactory.GetPlayerGrain(playerId).GetSummaryAsync(ct);

        return summary.Perks.HasFlag(PlayerPerkFlags.Trade);
    }

    private bool TryGetOpenSession(PlayerId playerId, out TradeSession session) =>
        _roomGrain._state.TradesByPlayerId.TryGetValue(playerId, out session!)
        && session.State == TradeStateType.Open;

    private bool TryGetPlayer(PlayerId playerId, out IRoomPlayer player) =>
        _roomGrain.PetModule.TryGetPlayer(playerId, out player);

    private Task SendOpenFailedAsync(
        PlayerId playerId,
        TradeOpenFailedType reason,
        string otherName,
        CancellationToken ct
    ) =>
        SendToPlayerAsync(
            playerId,
            new TradeOpenFailedEventPaserMessageComposer
            {
                Reason = reason,
                OtherPlayerName = otherName,
            },
            ct
        );

    private Task SendToBothAsync(TradeSession session, IComposer composer, CancellationToken ct) =>
        _roomGrain.SendComposerToPlayersAsync(
            [session.InitiatorId, session.PartnerId],
            composer,
            ct
        );

    private Task SendToPlayerAsync(PlayerId playerId, IComposer composer, CancellationToken ct) =>
        _roomGrain._grainFactory.GetPlayerPresenceGrain(playerId).SendComposerAsync(composer, ct);
}
