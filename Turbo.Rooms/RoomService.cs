using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Events;
using Turbo.Players.Policies;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Nft;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms;

/// <param name="policies">The hotel's plugins' room entry and placement policies; none decide without.</param>
/// <param name="events">Where a room entry is told once it is done; nowhere without.</param>
/// <param name="catalog">The builders club offers, whose furni a placement policy is told of.</param>
internal sealed partial class RoomService(
    ILogger<IRoomService> logger,
    IOptions<RoomConfig> roomConfig,
    IGrainFactory grainFactory,
    TimeProvider time,
    IGamePolicies? policies = null,
    EventSystem? events = null,
    ICatalogService? catalog = null,
    IFurnitureDefinitionProvider? definitions = null
) : IRoomService
{
    private readonly ILogger<IRoomService> _logger = logger;
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly TimeProvider _time = time;
    private readonly IGamePolicies _policies = policies ?? GamePolicies.None;

    public async Task<FollowFriendErrorCodeType?> FollowFriendAsync(
        PlayerId playerId,
        PlayerId friendId,
        CancellationToken ct
    )
    {
        if (playerId <= 0 || friendId <= 0)
            return FollowFriendErrorCodeType.NotFriend;

        if (
            !await _grainFactory
                .GetPlayerMessengerGrain(playerId)
                .IsFriendAsync(friendId, ct)
                .ConfigureAwait(false)
        )
            return FollowFriendErrorCodeType.NotFriend;

        var friendPresence = _grainFactory.GetPlayerPresenceGrain(friendId);

        if (!await friendPresence.HasActiveSessionAsync(ct).ConfigureAwait(false))
            return FollowFriendErrorCodeType.Offline;

        // The friend's privacy setting "Friends can follow me from room to room" is off:
        // friendlist.followerror.prevented.
        if (
            await _grainFactory
                .GetPlayerSettingsGrain(friendId)
                .GetSettingsAsync(ct)
                .ConfigureAwait(false) is
            { FollowingDisabled: true }
        )
            return FollowFriendErrorCodeType.Prevented;

        var activeRoom = await friendPresence.GetActiveRoomAsync(ct).ConfigureAwait(false);

        if (activeRoom.RoomId <= 0)
            return FollowFriendErrorCodeType.HotelView;

        var access = await CheckRoomEntryAccessAsync(
                playerId,
                activeRoom.RoomId,
                password: null,
                bypassDoor: false,
                ct
            )
            .ConfigureAwait(false);

        if (access != RoomEntryAccessType.Allowed)
            return FollowFriendErrorCodeType.Prevented;

        await _grainFactory
            .ForwardPlayerToRoomAsync(playerId, activeRoom.RoomId, ct)
            .ConfigureAwait(false);

        return null;
    }

    public async Task<RoomEntryAccessType> CheckRoomEntryAccessAsync(
        PlayerId playerId,
        RoomId roomId,
        string? password,
        bool bypassDoor,
        CancellationToken ct
    )
    {
        var room = _grainFactory.GetRoomGrain(roomId);

        var access = await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.ACCESS,
                roomId,
                () => room.CheckEntryAccessAsync(playerId, password, bypassDoor, ct)
            )
            .ConfigureAwait(false);

        // Activation already hydrates metadata, rights and bans. Rejected visitors need none
        // of the map, furniture or NPC state. Keep allowed loading before the caller clears
        // the player's current room, so a load failure leaves that session intact.
        if (access == RoomEntryAccessType.Allowed)
        {
            await RoomTelemetry
                .MeasureAsync(RoomTelemetry.ACTIVATE, roomId, () => room.EnsureRoomActiveAsync(ct))
                .ConfigureAwait(false);

            // A cold load can yield long enough for bans, capacity or deletion to change.
            access = await RoomTelemetry
                .MeasureAsync(
                    RoomTelemetry.ACCESS,
                    roomId,
                    () => room.CheckEntryAccessAsync(playerId, password, bypassDoor, ct)
                )
                .ConfigureAwait(false);
        }

        return access;
    }

    /// <summary>
    /// OpenConnection is sent first so the client leaves the hotel view, then the decision is
    /// applied: banned → CantConnect + CloseConnection, full → CantConnect alone (the client quits on it
    /// and its Quit closes the connection, as on Habbo), wrong or missing password →
    /// GenericError + CloseConnection, locked → doorbell ring (FlatAccessDenied when nobody can
    /// answer), allowed → the room is streamed.
    /// </summary>
    public async Task OpenRoomForPlayerIdAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomId roomId,
        RoomEntryAccessType access,
        CancellationToken ct
    )
    {
        if (playerId <= 0 || roomId <= 0)
            return;

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

        // Re-entering the room the player is already in is a full reload, not a no-op: by the time
        // this arrives the client has already torn its room view down and is waiting for the entry
        // sequence. Returning early here leaves it on a black screen forever.
        await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.PREPARE_PLAYER,
                roomId,
                async () =>
                {
                    await LeavePendingDoorbellAsync(playerPresence, playerId, roomId, ct)
                        .ConfigureAwait(false);
                    await playerPresence.ClearActiveRoomAsync(ct).ConfigureAwait(false);
                }
            )
            .ConfigureAwait(false);

        await _grainFactory
            .SendComposerToPlayerAsync(
                playerId,
                new OpenConnectionMessageComposer { RoomId = roomId },
                ct
            )
            .ConfigureAwait(false);

        var room = _grainFactory.GetRoomGrain(roomId);

        switch (access)
        {
            case RoomEntryAccessType.Banned:
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.Banned,
                            AdditionalInfo = string.Empty,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Full:
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.RoomFull,
                        },
                        ct,
                        closeConnection: false
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.PasswordRequired:
            case RoomEntryAccessType.InvalidPassword:
                await RejectEntryAsync(
                        playerPresence,
                        new GenericErrorMessage
                        {
                            ErrorCode = RoomGenericErrorType.InvalidPassword,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.HiddenByBuildersClub:
                // The pop-up says what the generic refusal cannot: the room is not gone, its
                // owner has let a Builders Club membership lapse.
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.NoEntry,
                        },
                        ct,
                        new NotificationDialogMessageComposer
                        {
                            NotificationType = BuildersClubNotifications.VISIT_DENIED_FOR_VISITOR,
                        }
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Closed:
            // Flash keeps a player who is not new from a new user lobby before asking; this is
            // the answer to one who asks anyway.
            case RoomEntryAccessType.NewUsersOnly:
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.NoEntry,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;

            // Habbo opens the entry like any other, with the player's NFT chat styles, then says
            // the room is not there and closes it; the client drops to the hotel view without an
            // alert (official client capture 2026-10-18, 2 of 2 direct entries).
            case RoomEntryAccessType.NotFound:
                await RejectEntryAsync(
                        playerPresence,
                        new NoSuchFlatEventMessageComposer { RoomId = roomId },
                        ct,
                        new UserNftChatStylesMessageComposer
                        {
                            ChatStyleIds = await _grainFactory
                                .GetPlayerSettingsGrain(playerId)
                                .GetOwnedNftChatStyleIdsAsync(ct)
                                .ConfigureAwait(false),
                        }
                    )
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Doorbell:
                if (
                    !await PassesEntryPolicyAsync(playerPresence, playerId, roomId, true, ct)
                        .ConfigureAwait(false)
                )
                    return;

                await RingDoorbellAsync(playerPresence, playerId, room, roomId, ct)
                    .ConfigureAwait(false);
                return;

            case RoomEntryAccessType.Allowed:
                if (
                    !await PassesEntryPolicyAsync(playerPresence, playerId, roomId, false, ct)
                        .ConfigureAwait(false)
                )
                    return;

                await playerPresence
                    .SetPendingRoomAsync(roomId, RoomEntryState.Approved, ct)
                    .ConfigureAwait(false);
                await EnterRoomAsync(ctx, playerPresence, room, roomId, ct).ConfigureAwait(false);

                events?.PublishCommitted(
                    new RoomEnteredEvent { PlayerId = playerId, RoomId = roomId }
                );
                return;

            default:
                _logger.LogWarning(
                    "Unhandled entry access {Access} for player {PlayerId} in room {RoomId}",
                    access,
                    playerId,
                    roomId
                );
                await RejectEntryAsync(
                        playerPresence,
                        new CantConnectMessageComposer
                        {
                            ErrorType = RoomConnectionErrorType.NoEntry,
                        },
                        ct
                    )
                    .ConfigureAwait(false);
                return;
        }
    }

    public async Task<bool> KickPlayerAsync(
        ActionContext ctx,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || targetId <= 0)
            return false;

        var room = _grainFactory.GetRoomGrain(ctx.RoomId);

        if (!await room.KickPlayerAsync(ctx, targetId, ct).ConfigureAwait(false))
            return false;

        await EvictPlayerAsync(ctx.RoomId, targetId, kicked: true, ct).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> BanPlayerAsync(
        ActionContext ctx,
        PlayerId targetId,
        RoomBanDurationType duration,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || targetId <= 0)
            return false;

        var room = _grainFactory.GetRoomGrain(ctx.RoomId);

        if (!await room.BanPlayerAsync(ctx, targetId, duration, ct).ConfigureAwait(false))
            return false;

        await EvictPlayerAsync(ctx.RoomId, targetId, kicked: true, ct).ConfigureAwait(false);

        return true;
    }

    public async Task DeleteRoomAsync(ActionContext ctx, CancellationToken ct)
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var room = _grainFactory.GetRoomGrain(ctx.RoomId);
        var players = await room.PrepareRoomDeletionAsync(ctx, ct).ConfigureAwait(false);

        if (players is null)
            return;

        // Sessions close before the row goes, so no presence grain re-enters a room mid-delete.
        foreach (var playerId in players.Value)
            await EvictPlayerAsync(ctx.RoomId, playerId, kicked: false, ct).ConfigureAwait(false);

        await room.CompleteRoomDeletionAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes a player's room session after the room grain has already dropped their avatar.
    /// The presence does it in one call that never re-enters the room; wired kicks use the
    /// same call from inside the room grain.
    /// </summary>
    private Task EvictPlayerAsync(
        RoomId roomId,
        PlayerId playerId,
        bool kicked,
        CancellationToken ct
    ) => _grainFactory.GetPlayerPresenceGrain(playerId).OnRemovedFromRoomAsync(roomId, kicked, ct);

    public async Task AnswerDoorbellAsync(
        ActionContext ctx,
        string playerName,
        bool accepted,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || string.IsNullOrWhiteSpace(playerName))
            return;

        var room = _grainFactory.GetRoomGrain(ctx.RoomId);
        var ringerId = await room.AnswerDoorbellAsync(ctx, playerName, accepted, ct)
            .ConfigureAwait(false);

        if (ringerId is null)
            return;

        var ringerPresence = _grainFactory.GetPlayerPresenceGrain(ringerId.Value);
        var pendingRoom = await ringerPresence.GetPendingRoomAsync(ct).ConfigureAwait(false);

        // The ringer moved on (entered elsewhere, quit, or disconnected) while waiting.
        if (
            pendingRoom.RoomId != ctx.RoomId
            || pendingRoom.State != RoomEntryState.RingingDoorbell
            || !await ringerPresence.HasActiveSessionAsync(ct).ConfigureAwait(false)
        )
        {
            await ringerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);

            return;
        }

        if (!accepted)
        {
            await ringerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);
            await _grainFactory
                .SendComposerToPlayerAsync(
                    ringerId.Value,
                    new FlatAccessDeniedMessageComposer
                    {
                        RoomId = ctx.RoomId,
                        Username = string.Empty,
                    },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        await RoomTelemetry
            .MeasureAsync(RoomTelemetry.ACTIVATE, ctx.RoomId, () => room.EnsureRoomActiveAsync(ct))
            .ConfigureAwait(false);

        await ringerPresence
            .SetPendingRoomAsync(ctx.RoomId, RoomEntryState.Approved, ct)
            .ConfigureAwait(false);

        // A nameless FlatAccessible closes the ringer's waiting view, then the room loads
        // (navigator/IncomingMessages.as onDoorOpened).
        await _grainFactory
            .SendComposerToPlayerAsync(
                ringerId.Value,
                new FlatAccessibleMessageComposer { RoomId = ctx.RoomId, Username = string.Empty },
                ct
            )
            .ConfigureAwait(false);

        await EnterRoomAsync(
                ActionContext.CreateForPlayer(ringerId.Value, ctx.RoomId),
                ringerPresence,
                room,
                ctx.RoomId,
                ct
            )
            .ConfigureAwait(false);
    }

    public async Task CloseRoomForPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        if (playerId <= 0)
            return;

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

        await LeavePendingDoorbellAsync(playerPresence, playerId, RoomId.Invalid, ct)
            .ConfigureAwait(false);
        await playerPresence.ClearActiveRoomAsync(ct).ConfigureAwait(false);

        await _grainFactory
            .SendComposerToPlayerAsync(playerId, new CloseConnectionMessageComposer(), ct)
            .ConfigureAwait(false);
    }

    private async Task RingDoorbellAsync(
        IPlayerPresenceGrain playerPresence,
        PlayerId playerId,
        IRoomGrain room,
        RoomId roomId,
        CancellationToken ct
    )
    {
        var player = await _grainFactory
            .GetPlayerGrain(playerId)
            .GetSummaryAsync(ct)
            .ConfigureAwait(false);

        await playerPresence
            .SetPendingRoomAsync(roomId, RoomEntryState.RingingDoorbell, ct)
            .ConfigureAwait(false);

        var notified = await room.RingDoorbellAsync(playerId, player.Name, ct)
            .ConfigureAwait(false);

        // The ringer is told to wait with a nameless Doorbell: the client shows its "waiting for
        // an answer" view for that one only (navigator/IncomingMessages.as onDoorbell).
        if (notified)
        {
            await _grainFactory
                .SendComposerToPlayerAsync(
                    playerId,
                    new DoorbellMessageComposer { Username = string.Empty },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        await playerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);
        await _grainFactory
            .SendComposerToPlayerAsync(
                playerId,
                new FlatAccessDeniedMessageComposer { RoomId = roomId, Username = string.Empty },
                ct
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the player from the doorbell queue of any room they are still waiting on, unless
    /// that room is the one they are entering now.
    /// </summary>
    private async Task LeavePendingDoorbellAsync(
        IPlayerPresenceGrain playerPresence,
        PlayerId playerId,
        RoomId exceptRoomId,
        CancellationToken ct
    )
    {
        var pendingRoom = await playerPresence.GetPendingRoomAsync(ct).ConfigureAwait(false);

        if (pendingRoom.RoomId <= 0 || pendingRoom.RoomId == exceptRoomId)
            return;

        if (pendingRoom.State == RoomEntryState.RingingDoorbell)
            await _grainFactory
                .GetRoomGrain(pendingRoom.RoomId)
                .RemoveDoorbellRingerAsync(playerId, ct)
                .ConfigureAwait(false);

        await playerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asks the room entry policies (<see cref="IRoomEntryPolicy"/>) about an entry the room
    /// allows. A refusal is told as the room's own "no entry", after the policy's reason when it
    /// gives one; a redirect closes this entry and forwards the player to the other room, which
    /// they enter as any other. False when the player does not go on into this room.
    /// </summary>
    private async Task<bool> PassesEntryPolicyAsync(
        IPlayerPresenceGrain playerPresence,
        PlayerId playerId,
        RoomId roomId,
        bool doorbell,
        CancellationToken ct
    )
    {
        var decision = await _policies
            .DecideRoomEntryAsync(new RoomEntryRequest(playerId, roomId, doorbell), ct)
            .ConfigureAwait(false);

        if (decision.Allowed)
            return true;

        var notice = string.IsNullOrEmpty(decision.Reason)
            ? null
            : PolicyRefusals.Notification(PolicyRefusals.ROOM_ENTRY, decision.Reason);

        if (decision.RedirectRoomId is { } redirect && redirect > 0 && redirect != roomId)
        {
            await RejectEntryAsync(
                    playerPresence,
                    new CloseConnectionMessageComposer(),
                    ct,
                    notice,
                    closeConnection: false
                )
                .ConfigureAwait(false);
            await _grainFactory
                .ForwardPlayerToRoomAsync(playerId, redirect, ct)
                .ConfigureAwait(false);

            return false;
        }

        await RejectEntryAsync(
                playerPresence,
                new CantConnectMessageComposer { ErrorType = RoomConnectionErrorType.NoEntry },
                ct,
                notice
            )
            .ConfigureAwait(false);

        return false;
    }

    /// <param name="notice">Sent before the refusal, in the same batch: what explains it, or what Habbo sends ahead of it.</param>
    /// <param name="closeConnection">False when the client's own Quit, which it sends on the refusal, closes it.</param>
    private static async Task RejectEntryAsync(
        IPlayerPresenceGrain playerPresence,
        IComposer error,
        CancellationToken ct,
        IComposer? notice = null,
        bool closeConnection = true
    )
    {
        await playerPresence.ClearPendingRoomAsync(ct).ConfigureAwait(false);

        List<IComposer> composers = notice is null ? [error] : [notice, error];

        if (closeConnection)
            composers.Add(new CloseConnectionMessageComposer());

        await playerPresence.SendComposerAsync(composers, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Streams the room to a player whose entry has already been approved and whose client has
    /// already received OpenConnection.
    /// </summary>
    private async Task EnterRoomAsync(
        ActionContext ctx,
        IPlayerPresenceGrain playerPresence,
        IRoomGrain room,
        RoomId roomId,
        CancellationToken ct
    )
    {
        // One call for everything the room shows, taken in one of its turns.
        var view = await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.ENTRY_VIEW,
                roomId,
                () => room.GetEntryViewAsync(ctx.PlayerId, ct)
            )
            .ConfigureAwait(false);
        var snapshot = view.Room;

        // The room as one batch to the player (RoomEntryComposers.RoomOf). The rest of the entry,
        // from the player's own avatar to the raid protection, comes from the room as it makes
        // the avatar (SetActiveRoomAsync).
        var nftChatStyleIds = await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .GetOwnedNftChatStyleIdsAsync(ct)
            .ConfigureAwait(false);

        var purchasableChatStyleIds = await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .GetOwnedPurchasableChatStyleIdsAsync(ct)
            .ConfigureAwait(false);

        var composers = new List<IComposer>
        {
            // Habbo opens every entry with the player's NFT chat styles (an empty list for an account
            // without any) right before RoomReady (hhus, 2026-10-13, 17 of 17 entries); the client
            // offers an NFT bubble only to an owner it names (RoomChatInputView.hasNftChatStyle).
            new UserNftChatStylesMessageComposer { ChatStyleIds = nftChatStyleIds },
        };

        // The purchasable styles the player owns, which the style selector offers only to an
        // owner it was told of. Habbo never sent it to the recorded accounts, which own none (325
        // official client captures), so it goes only to an owner: an entry of everyone else stays
        // as recorded. Where Habbo sends it is unrecorded; with the NFT styles is inferred.
        if (purchasableChatStyleIds is { Count: > 0 })
            composers.Add(
                new UserPurchasableChatStylesMessageComposer
                {
                    ChatStyleIds = purchasableChatStyleIds,
                }
            );

        composers.AddRange(RoomEntryComposers.RoomOf(roomId, view, _roomConfig));

        // A batch for one player is the one direct presence send AGENTS.md allows.
        await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.INITIAL_PACKETS,
                roomId,
                () => playerPresence.SendComposerAsync(composers, ct)
            )
            .ConfigureAwait(false);

        await RoomTelemetry
            .MeasureAsync(
                RoomTelemetry.MEMBERSHIP,
                roomId,
                () => playerPresence.SetActiveRoomAsync(roomId, snapshot.OwnerId, ct)
            )
            .ConfigureAwait(false);

        // Last: the client takes a poll offer only for the room session it has (PollHandler
        // .onPollOfferEvent). Where in the entry Habbo sends it is unrecorded.
        await _grainFactory
            .GetPlayerRoomPollGrain(ctx.PlayerId)
            .OfferAsync(roomId, ct)
            .ConfigureAwait(false);
    }

    public async Task ClickTileAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain.ClickTileAsync(ctx, targetX, targetY, ct).ConfigureAwait(false);
        await roomGrain.WalkAvatarToAsync(ctx, targetX, targetY, ct).ConfigureAwait(false);
    }

    public async Task PickupItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        bool confirmed,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain.RemoveItemByIdAsync(ctx, itemId, confirmed, ct).ConfigureAwait(false);
    }

    public async Task UseItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain.UseItemByIdAsync(ctx, itemId, ct, param).ConfigureAwait(false);
    }

    public async Task ClickItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || itemId <= 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain.ClickItemByIdAsync(ctx, itemId, ct, param).ConfigureAwait(false);
    }
}
