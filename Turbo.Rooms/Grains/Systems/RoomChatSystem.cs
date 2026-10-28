using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Bot;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Primitives.Texts;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Room chat pipeline. Every message becomes a <see cref="PlayerChatEvent"/> that is first offered
/// to the global event pipeline (chat commands, filters), then published to the room event module
/// so room listeners such as the wired system react to it, then broadcast.
/// </summary>
public sealed class RoomChatSystem(RoomGrain roomGrain)
    : RoomGrainComponent(roomGrain),
        IRoomEventListener
{
    // What the client is sent for a line no player typed, so it has no message to track.
    private const int NO_TRACKING_ID = -1;

    private readonly Dictionary<PlayerId, Queue<long>> _chatTimestampsByPlayerId = [];
    private readonly Dictionary<PlayerId, long> _floodMutedUntilByPlayerId = [];

    private enum FloodKind
    {
        Chat,
        Command,
    }

    public async Task<bool> SendChatFromPlayerAsync(
        ActionContext ctx,
        RoomChatType chatType,
        string text,
        int styleId,
        int trackingId,
        string? recipientName,
        CancellationToken ct
    )
    {
        if (!AvatarModule.TryGetPlayer(ctx.PlayerId, out var speaker))
            return false;

        var maxLength = RoomConfig.ChatMaxLength;

        if (text.Length == 0)
            return false;

        if (await IsMutedAsync(ctx.PlayerId, ct))
            return false;

        // Match the original line before chat truncation or filtering. Truncating first could turn
        // an overlength command into a shorter, executable one.
        if (
            chatType != RoomChatType.Whisper
            && CommandSystem.TryMatch(text, out var command, out var arguments)
        )
        {
            var flooded = await IsFloodingAsync(ctx.PlayerId, FloodKind.Command, ct);

            if (flooded)
            {
                await CommandSystem.ExecuteAsync(
                    ctx,
                    speaker,
                    command,
                    arguments,
                    flooded: true,
                    ct
                );

                return true;
            }

            if (maxLength > 0 && text.Length > maxLength)
            {
                await CommandSystem.ReplyInputRejectedAsync(
                    speaker,
                    command,
                    CommandReplyKeys.TOO_LONG,
                    ct
                );

                return true;
            }

            await CommandSystem.ExecuteAsync(ctx, speaker, command, arguments, flooded: false, ct);

            return true;
        }

        // A chat limit of zero means none, which ClientText would read as "nothing fits".
        text = ClientText.Truncate(text, maxLength > 0 ? maxLength : int.MaxValue);

        if (text.Length == 0)
            return false;

        var typed = text;

        text = ModerationModule.ApplyFilter(text);

        if (text.Length == 0)
            return false;

        if (await IsFloodingAsync(ctx.PlayerId, FloodKind.Chat, ct))
            return false;

        styleId = await ResolveStyleIdAsync(speaker, styleId, ct);

        // A whisper to a name not in the room still goes out: its speaker sees it and wired reads
        // it (official client capture 2026-10-20: a whisper to an absent player came back to its
        // speaker and fired Says Keyword, 2 of 2).
        var recipient =
            chatType == RoomChatType.Whisper ? FindPlayerAvatarByName(recipientName) : null;

        var evt = new PlayerChatEvent
        {
            RoomId = State.RoomId,
            CausedBy = ctx,
            PlayerId = ctx.PlayerId,
            ObjectId = speaker.ObjectId,
            ChatType = chatType,
            Text = text,
            UnfilteredText = typed == text ? null : typed,
            StyleId = styleId,
            TrackingId = trackingId,
            Gesture = GetGestureForText(text),
            TargetPlayerId = recipient?.PlayerId,
            RecipientName = chatType == RoomChatType.Whisper ? recipientName : null,
            SpeakerX = speaker.X,
            SpeakerY = speaker.Y,
            WiredMayRead =
                chatType != RoomChatType.Whisper || await WiredMayReadWhisperAsync(speaker, ct),
        };

        await _roomGrain._eventSystem.PublishAsync(evt, ct);

        if (evt.IsCancelled)
            return true;

        // A line a "hide" User Says Keyword box fires on is not shown to the room: its speaker
        // alone gets it back, as a whisper over their own avatar (hhus, 2026-10-13: Chat and
        // Shout both came back as Whisper with the speaker as receiver). The wired still fires.
        // A box whose conditions fail does not hide it (official client video, nitro-tester
        // 2026-10-22: shown to the room beside the furni, whispered back to the speaker on it).
        var hidden = chatType != RoomChatType.Whisper && await IsHiddenByWiredAsync(evt, ct);

        // The room hears the line, and runs the stacks it fires, before the line goes out: a
        // Says stack's whisper and the bot lines it makes reach the room before the speaker's
        // own Chat, in the same ms (official client capture 2026-10-25, LAB-W3 wlab9
        // trigger.0.R1b, 21/21; the stack's states before the echo, 2026-10-14, 3/3).
        await _roomGrain.PublishRoomEventAsync(evt, ct);
        await _roomGrain.RunRaisedWiredInCallAsync(ct);

        if (hidden)
            await GrainFactory.SendComposerToPlayerAsync(
                speaker.PlayerId,
                CreateComposer(
                    RoomChatType.Whisper,
                    speaker.ObjectId,
                    evt.Text,
                    evt.Gesture,
                    evt.StyleId,
                    evt.TrackingId,
                    receiverRoomIndex: speaker.ObjectId,
                    bubbleWidth: null,
                    hideUsername: false
                ),
                ct
            );
        else
            await BroadcastAsync(evt, speaker, recipient, ct);

        TurnHeadsTowards(speaker, chatType, recipient);

        await PetModule.HandleChatAsync(evt, ct);

        // Kept by the room and handed to persistence with the rest of what changed, rather than
        // a grain call per line.
        if (RoomConfig.ChatlogEnabled)
            _roomGrain.QueueChatlog(
                new RoomChatlogSnapshot
                {
                    RoomId = evt.RoomId,
                    PlayerId = evt.PlayerId,
                    TargetPlayerId = evt.TargetPlayerId,
                    Text = evt.Text,
                }
            );

        if (
            chatType == RoomChatType.Whisper
            && evt.WiredMayRead
            && WiredConfig.NotifyWhisperRead
            && await IsReadByWiredAsync(evt, ct)
        )
            await GrainFactory.SendComposerToPlayerAsync(speaker.PlayerId, WhisperReadNotice(), ct);

        return true;
    }

    /// <summary>
    /// Whether wired may read a whisper: the hotel lets it, the room has a Says Keyword box to
    /// read it with, and the whisperer has not ticked "Disable whispering to Wired"
    /// (wiredWhisperDisabled, saved by WiredSetPreferences). A settings read that fails counts as
    /// opted out.
    /// </summary>
    private async Task<bool> WiredMayReadWhisperAsync(IRoomPlayer speaker, CancellationToken ct)
    {
        if (
            !WiredConfig.ReadWhispers
            || !State.ItemsById.Values.Any(x => x.Logic is WiredTriggerHabboSaysKeyword)
        )
            return false;

        try
        {
            var settings = await _roomGrain
                ._grainFactory.GetPlayerSettingsGrain(speaker.PlayerId)
                .GetSettingsAsync(ct);

            return !settings.WiredWhisperDisabled;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Could not read the wired whisper preference of player {PlayerId} in room {RoomId}",
                speaker.PlayerId,
                _roomGrain.RoomId
            );

            return false;
        }
    }

    private async Task<bool> IsReadByWiredAsync(PlayerChatEvent evt, CancellationToken ct)
    {
        foreach (var item in State.ItemsById.Values)
        {
            if (
                item.Logic is WiredTriggerHabboSaysKeyword trigger
                && await trigger.FiresForAsync(evt, ct)
            )
                return true;
        }

        return false;
    }

    // The text Habbo has for it; the dialog type is the one its other wired notices use.
    private static NotificationDialogMessageComposer WhisperReadNotice() =>
        new()
        {
            NotificationType = "wired.error",
            Parameters = ImmutableDictionary<string, string>.Empty.Add(
                "message",
                "${room.error.wired.whisper_read}"
            ),
        };

    /// <summary>
    /// A bubble over an avatar that is not a player typing: a bot's line, a pet's reply, a wired
    /// message, the room telling a player why they were muted. The words are filtered like
    /// anything players read, but it is not chat: no flood check, no commands, no chat log. Every
    /// such bubble is built here, so a change to what a bubble carries is one edit.
    ///
    /// What a bot says or shouts to the room is heard by wired as a user speaking
    /// (<see cref="BotChatEvent"/>): Says Keyword fired for a bot's line, once per line, with the
    /// bot as the triggering user (official client capture 2026-10-24, W3 lab trigger.0.R2, 7/7
    /// fires, 4/4 triggerer). A bot's whisper is not offered (inference: only talk was recorded).
    /// </summary>
    public async Task SayAsAvatarAsync(
        IRoomAvatar speaker,
        string text,
        AvatarSpeech speech,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = ModerationModule.ApplyFilter(text);

        if (speech.OnlyFor is { } listener)
        {
            await GrainFactory.SendComposerToPlayerAsync(
                listener.PlayerId,
                CreateComposer(
                    RoomChatType.Whisper,
                    speaker.ObjectId,
                    text,
                    AvatarGestureType.None,
                    speech.StyleId,
                    NO_TRACKING_ID,
                    listener.ObjectId,
                    speech.BubbleWidth,
                    speech.HideUsername
                ),
                ct
            );

            return;
        }

        var composer = CreateComposer(
            speech.Shout ? RoomChatType.Shout : RoomChatType.Chat,
            speaker.ObjectId,
            text,
            AvatarGestureType.None,
            speech.StyleId,
            NO_TRACKING_ID,
            receiverRoomIndex: null,
            speech.BubbleWidth,
            speech.HideUsername
        );

        // The room setting "Mute all pets" (navigator.roomsettings.mute_all_pets, RoomSettingsCtrl
        // mute_all_pets_checkbox): no one in the room is sent what pets say. Inference from the
        // setting's name; not recorded.
        if (speaker is IRoomPet && State.RoomSnapshot.MuteAllPets)
            return;

        // ":mutepets Ignore pet chats" and ":mutebots Ignore bot chats" (habbopages/chat/commands):
        // whoever said it is not sent what pets, or bots, say.
        var mutedBy = speaker switch
        {
            IRoomPet => State.PetsMutedBy,
            IRoomBot => State.BotsMutedBy,
            _ => null,
        };

        if (mutedBy is { Count: > 0 })
            await GrainFactory.SendComposerToPlayersAsync(
                [.. AvatarModule.Players.Select(x => x.PlayerId).Where(x => !mutedBy.Contains(x))],
                composer,
                ct
            );
        else
            await _roomGrain.SendComposerToRoomAsync(composer, ct);

        if (speaker is IRoomBot)
            await _roomGrain.PublishRoomEventAsync(
                new BotChatEvent
                {
                    RoomId = State.RoomId,
                    CausedBy = ActionContext.CreateForSystem(State.RoomId),
                    ObjectId = speaker.ObjectId,
                    ChatType = speech.Shout ? RoomChatType.Shout : RoomChatType.Chat,
                    Text = text,
                },
                ct
            );
    }

    /// <summary>
    /// The room telling one player something: a whisper over their own avatar that nobody else
    /// sees (why they were muted, a parting word before a kick).
    /// </summary>
    public Task WhisperToPlayerAsync(IRoomPlayer player, string text, CancellationToken ct) =>
        SayAsAvatarAsync(player, text, new AvatarSpeech { OnlyFor = player }, ct);

    public Task<bool> SetAvatarTypingAsync(ActionContext ctx, bool isTyping, CancellationToken ct)
    {
        if (!AvatarModule.TryGetPlayer(ctx.PlayerId, out var avatar))
            return Task.FromResult(false);

        _roomGrain.SendComposerToRoomAndForget(
            new UserTypingMessageComposer { UserId = avatar.ObjectId, IsTyping = isTyping }
        );

        return Task.FromResult(true);
    }

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is PlayerLeftEvent leftEvt)
        {
            _chatTimestampsByPlayerId.Remove(leftEvt.PlayerId);
            _floodMutedUntilByPlayerId.Remove(leftEvt.PlayerId);
        }

        return Task.CompletedTask;
    }

    private async Task<bool> IsHiddenByWiredAsync(PlayerChatEvent evt, CancellationToken ct)
    {
        foreach (var item in State.ItemsById.Values)
        {
            if (
                item.Logic is WiredTriggerHabboSaysKeyword trigger
                && await trigger.HidesAsync(evt, ct)
                && await WiredSystem.WouldPassAsync(trigger, evt, ct)
            )
                return true;
        }

        return false;
    }

    private async Task BroadcastAsync(
        PlayerChatEvent evt,
        IRoomPlayer speaker,
        IRoomPlayer? recipient,
        CancellationToken ct
    )
    {
        var composer = CreateComposer(evt, evt.Text);

        if (evt.ChatType != RoomChatType.Whisper)
        {
            var masked = MaskForListeners(evt, speaker, AvatarModule.Players);

            if (masked.Count == 0)
                await _roomGrain.SendComposerToRoomAsync(composer, ct);
            else
                await _roomGrain.SendComposerToRoomExceptAsync(
                    composer,
                    [.. masked.Select(x => x.PlayerId)],
                    ct
                );

            foreach (var (playerId, text) in masked)
                await GrainFactory.SendComposerToPlayerAsync(
                    playerId,
                    CreateComposer(evt, text),
                    ct
                );

            return;
        }

        var targets = new HashSet<PlayerId> { speaker.PlayerId };

        if (recipient is not null)
        {
            var masked = MaskForListeners(evt, speaker, [recipient]);

            if (masked.Count == 0)
                targets.Add(recipient.PlayerId);
            else
                await GrainFactory.SendComposerToPlayerAsync(
                    recipient.PlayerId,
                    CreateComposer(evt, masked[0].Text),
                    ct
                );
        }

        await GrainFactory.SendComposerToPlayersAsync(targets, composer, ct);
    }

    /// <summary>
    /// The listeners whose own word filter changes the line, each with the line as they see it:
    /// every word on their list, in any case, replaced by the hotel's replacement (official
    /// client capture, 2026-10-13: a player filtering "pineapple" was sent Chat "I like bobba
    /// pizza", Shout "bobba shout" for "PINEAPPLE shout" and Whisper "secret bobba", while the
    /// speaker's own client showed what they typed). Words are matched as the room filter
    /// matches them (<see cref="FilterWords"/>). The speaker always gets their own line as typed.
    /// </summary>
    private List<(PlayerId PlayerId, string Text)> MaskForListeners(
        PlayerChatEvent evt,
        IRoomPlayer speaker,
        IEnumerable<IRoomPlayer> listeners
    )
    {
        var masked = new List<(PlayerId PlayerId, string Text)>();

        foreach (var listener in listeners)
        {
            if (listener.PlayerId == speaker.PlayerId || listener.CustomFilterWords.Count == 0)
                continue;

            var text = FilterWords.Apply(
                evt.Text,
                listener.CustomFilterWords,
                _roomGrain._wordFilter.Replacement
            );

            if (!string.Equals(text, evt.Text, StringComparison.Ordinal))
                masked.Add((listener.PlayerId, text));
        }

        return masked;
    }

    private static ChatMessageComposer CreateComposer(PlayerChatEvent evt, string text) =>
        CreateComposer(
            evt.ChatType,
            evt.ObjectId,
            text,
            evt.Gesture,
            evt.StyleId,
            evt.TrackingId,
            receiverRoomIndex: null,
            bubbleWidth: null,
            hideUsername: false
        );

    /// <summary>
    /// Every chat bubble the room sends is built here, typed or not: the three packets differ
    /// only in their header. The copies this replaced had drifted on the trailing fields.
    /// </summary>
    private static ChatMessageComposer CreateComposer(
        RoomChatType chatType,
        RoomObjectId objectId,
        string text,
        AvatarGestureType gesture,
        int styleId,
        int trackingId,
        int? receiverRoomIndex,
        int? bubbleWidth,
        bool hideUsername
    ) =>
        chatType switch
        {
            RoomChatType.Shout => new ShoutMessageComposer
            {
                ObjectId = objectId,
                Text = text,
                Gesture = gesture,
                StyleId = styleId,
                Links = [],
                TrackingId = trackingId,
                ReceiverRoomIndex = receiverRoomIndex,
                ChatBubbleWidthOverride = bubbleWidth,
                HideUsername = hideUsername,
            },
            RoomChatType.Whisper => new WhisperMessageComposer
            {
                ObjectId = objectId,
                Text = text,
                Gesture = gesture,
                StyleId = styleId,
                Links = [],
                TrackingId = trackingId,
                ReceiverRoomIndex = receiverRoomIndex,
                ChatBubbleWidthOverride = bubbleWidth,
                HideUsername = hideUsername,
            },
            _ => new ChatMessageComposer
            {
                ObjectId = objectId,
                Text = text,
                Gesture = gesture,
                StyleId = styleId,
                Links = [],
                TrackingId = trackingId,
                ReceiverRoomIndex = receiverRoomIndex,
                ChatBubbleWidthOverride = bubbleWidth,
                HideUsername = hideUsername,
            },
        };

    /// <summary>
    /// Listeners look at whoever talks and back to their body a while later, as Habbo does it
    /// (official client capture, hhus 2026-10-17, 30 lines): the head turns on the next update,
    /// body and posture kept, a floor sitter's too, and looks back 10 ticks (~5.0 s) after it,
    /// counted again from a later line. Chat, shout and a whisper's recipient alike, at any
    /// distance seen (Chebyshev 12). A listener on its last step turns as it arrives; the talker
    /// never turns, nor does a sleeping listener (2/2), which the line leaves asleep. A head that
    /// already faces the speaker gets no update for the line, only the look-back (2/2). One held by
    /// a seat keeps its head on the body.
    /// </summary>
    private void TurnHeadsTowards(
        IRoomPlayer speaker,
        RoomChatType chatType,
        IRoomPlayer? recipient
    )
    {
        var range = RoomConfig.ChatLookAtRange;
        var revertAtMs = NowMs() + RoomConfig.ChatLookAtRevertMs;

        foreach (var avatar in AvatarModule.Avatars)
        {
            if (avatar.ObjectId == speaker.ObjectId)
                continue;

            if (chatType == RoomChatType.Whisper && avatar.ObjectId != recipient?.ObjectId)
                continue;

            if (avatar.IsIdle)
                continue;

            var (x, y) = (avatar.X, avatar.Y);

            // A walk faces its path; only the step onto its goal, already facing its last
            // direction, takes the look (1/1).
            if (avatar.IsWalking)
            {
                if (avatar.TilePath.Count > 0 || avatar.NextTileId < 0)
                    continue;

                (x, y) = MapModule.GetTileXY(avatar.NextTileId);
            }

            if (x == speaker.X && y == speaker.Y)
                continue;

            // A limit of zero means none, as on Habbo.
            if (
                chatType == RoomChatType.Chat
                && range > 0
                && Math.Max(Math.Abs(x - speaker.X), Math.Abs(y - speaker.Y)) > range
            )
                continue;

            if (GetChatHeadRotation(x, y, avatar.Rotation, speaker.X, speaker.Y) is not { } head)
                continue;

            // Straight ahead with the head there already: nothing to look at, nothing sent.
            if (head == avatar.Rotation && avatar.HeadRotation == head)
                continue;

            // A seat holds the head on the body: where a standing listener would turn, the room
            // gets one unchanged update on the turn's tick and no look-back (3/3); where it would
            // not, nothing (2/2). A bed holds it the same way (inferred, not recorded).
            if (AvatarModule.IsHeldByFurni(avatar))
            {
                if (head != avatar.Rotation)
                    avatar.MarkDirty();

                continue;
            }

            avatar.SetHeadRotation(head);
            avatar.HeadRevertAtMs = revertAtMs;
        }
    }

    /// <summary>
    /// Where a listener facing <paramref name="body"/> looks for a speaker, or null when the
    /// speaker is behind it. The client draws whatever head the server sends (AS3
    /// AvatarImage.setDirectionWithOffset), so the rule is the server's. Fitted to 14 official
    /// samples (hhus 2026-10-17, inference where they leave room): the speaker's octant is the
    /// one whose 45 degree sector ends at its bearing (ahead within 33.7 degrees anticlockwise is
    /// still ahead, 26.6 degrees clockwise is not); the head moves at most one octant from the
    /// body, so a speaker 90 degrees aside gets the octant toward it; past that, nothing.
    /// </summary>
    internal static Rotation? GetChatHeadRotation(
        int x,
        int y,
        Rotation body,
        int speakerX,
        int speakerY
    )
    {
        var bearing = Math.Atan2(speakerX - x, y - speakerY) * 180 / Math.PI;

        if (bearing < 0)
            bearing += 360;

        // The epsilon keeps an exact diagonal or straight line in its own octant.
        var toward = (int)Math.Ceiling(bearing / 45 - 1e-9) % 8;

        return ((toward - (int)body + 8) % 8) switch
        {
            0 => body,
            1 or 2 => body.Rotate(1),
            6 or 7 => body.Rotate(-1),
            _ => null,
        };
    }

    private async Task<bool> IsMutedAsync(PlayerId playerId, CancellationToken ct)
    {
        var remainingSeconds = ModerationModule.GetRemainingMuteSeconds(playerId);

        if (remainingSeconds > 0)
        {
            await GrainFactory.SendComposerToPlayerAsync(
                playerId,
                new RemainingMutePeriodMessageComposer { SecondsRemaining = remainingSeconds },
                ct
            );

            return true;
        }

        if (await IsHotelMutedAsync(playerId, ct))
            return true;

        return await ModerationModule.IsSilencedByRoomMuteAsync(playerId);
    }

    /// <summary>
    /// A hotel mute: the player does not hold <c>chat.speak</c>, which the default group grants and a
    /// mute denies. A temporary one tells the player how long is left, as a room mute does. An
    /// avatar whose permissions could not be read holds nothing, and is not muted for that.
    /// </summary>
    private async Task<bool> IsHotelMutedAsync(PlayerId playerId, CancellationToken ct)
    {
        if (
            !AvatarModule.TryGetPlayer(playerId, out var player)
            || ReferenceEquals(player.Permissions, ResolvedPermissionsSnapshot.EMPTY)
            || SecurityModule.HasPermission(player, PermissionNodes.Chat.SPEAK)
        )
            return false;

        var check = await _roomGrain
            ._grainFactory.GetPlayerPermissionGrain(playerId)
            .ExplainAsync(PermissionNodes.Chat.SPEAK, ct);

        if (check.Decision?.ExpiresAt is { } until && until > UtcNow.UtcDateTime)
            await GrainFactory.SendComposerToPlayerAsync(
                playerId,
                new RemainingMutePeriodMessageComposer
                {
                    SecondsRemaining = (int)Math.Ceiling((until - UtcNow.UtcDateTime).TotalSeconds),
                },
                ct
            );

        return true;
    }

    private int GetFloodMaxMessages()
    {
        var config = RoomConfig;

        return State.RoomSnapshot.ChatProtection switch
        {
            ChatFloodSensitivityType.Extra => config.ChatFloodMaxMessagesExtraSensitivity,
            ChatFloodSensitivityType.Minimal => config.ChatFloodMaxMessagesMinimalSensitivity,
            _ => config.ChatFloodMaxMessagesNormalSensitivity,
        };
    }

    /// <summary>
    /// Commands and chat share one window and one count, so ordinary use of both is held to the
    /// room's limit together. They differ in what going over costs: a chat line starts the chat
    /// mute and tells the client (which locks the input), a command is only dropped. A fast
    /// <c>:commands</c> must not take a player's voice away.
    /// </summary>
    private async Task<bool> IsFloodingAsync(
        PlayerId playerId,
        FloodKind kind,
        CancellationToken ct
    )
    {
        var config = RoomConfig;
        var now = NowMs();

        if (_floodMutedUntilByPlayerId.TryGetValue(playerId, out var mutedUntil))
        {
            if (now < mutedUntil)
                return true;

            _floodMutedUntilByPlayerId.Remove(playerId);
        }

        var maxMessages = GetFloodMaxMessages();

        if (maxMessages <= 0 || config.ChatFloodWindowMs <= 0)
            return false;

        if (!_chatTimestampsByPlayerId.TryGetValue(playerId, out var timestamps))
        {
            timestamps = new Queue<long>();
            _chatTimestampsByPlayerId[playerId] = timestamps;
        }

        while (timestamps.Count > 0 && now - timestamps.Peek() > config.ChatFloodWindowMs)
            timestamps.Dequeue();

        if (timestamps.Count >= maxMessages)
        {
            if (kind == FloodKind.Command)
                return true;

            _floodMutedUntilByPlayerId[playerId] = now + config.ChatFloodMuteMs;
            timestamps.Clear();

            await GrainFactory.SendComposerToPlayerAsync(
                playerId,
                new FloodControlMessageComposer
                {
                    Seconds = (int)Math.Ceiling(config.ChatFloodMuteMs / 1000d),
                },
                ct
            );

            return true;
        }

        timestamps.Enqueue(now);

        return false;
    }

    /// <summary>
    /// The bubble a player's line goes out in: the one they picked when they may speak with it,
    /// otherwise the default. The client only offers styles it believes are allowed, so anything
    /// else is a lapsed club or a modified client, and the line is still worth sending.
    /// </summary>
    private async Task<int> ResolveStyleIdAsync(
        IRoomPlayer speaker,
        int styleId,
        CancellationToken ct
    )
    {
        if (styleId == ChatStyles.DEFAULT_STYLE_ID)
            return styleId;

        var style = _roomGrain._chatStyleProvider.GetChatStyle(styleId);

        if (style is null)
            return ChatStyles.DEFAULT_STYLE_ID;

        // Only a purchasable style costs a grain call, and only then is ownership the question.
        var ownsStyle =
            style.Purchasable
            && await _roomGrain
                ._grainFactory.GetPlayerSettingsGrain(speaker.PlayerId)
                .OwnsChatStyleAsync(styleId, ct);

        var canSpeakWith = ChatStyles.CanSpeakWith(
            style,
            hasClub: speaker.HabboClubExpiresAt is { } expiresAt && expiresAt > UtcNow.UtcDateTime,
            isAmbassador: SecurityModule.HasPermission(speaker, PermissionNodes.Role.AMBASSADOR),
            isStaff: SecurityModule.HasPermission(speaker, PermissionNodes.Chat.STYLE_STAFF),
            ownsStyle
        );

        return canSpeakWith ? styleId : ChatStyles.DEFAULT_STYLE_ID;
    }

    private IRoomPlayer? FindPlayerAvatarByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return AvatarModule.Players.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static AvatarGestureType GetGestureForText(string text)
    {
        if (
            text.Contains(">:(", StringComparison.Ordinal)
            || text.Contains(":@", StringComparison.Ordinal)
        )
            return AvatarGestureType.Angry;

        if (
            text.Contains(":(", StringComparison.Ordinal)
            || text.Contains(":-(", StringComparison.Ordinal)
        )
            return AvatarGestureType.Sad;

        if (text.Contains(":o", StringComparison.OrdinalIgnoreCase))
            return AvatarGestureType.Surprised;

        if (
            text.Contains(":)", StringComparison.Ordinal)
            || text.Contains(":-)", StringComparison.Ordinal)
            || text.Contains(":D", StringComparison.Ordinal)
        )
            return AvatarGestureType.Smile;

        return AvatarGestureType.None;
    }
}
