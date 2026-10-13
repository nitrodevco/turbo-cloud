using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
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
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Primitives.Texts;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Room chat pipeline. Every message becomes a <see cref="PlayerChatEvent"/> that is first offered
/// to the global event pipeline (chat commands, filters), then broadcast, then published to the
/// room event module so room listeners such as the wired system can react to it.
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

        var maxLength = _roomGrain._roomConfig.ChatMaxLength;

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

        text = ModerationModule.ApplyFilter(text);

        if (text.Length == 0)
            return false;

        if (await IsFloodingAsync(ctx.PlayerId, FloodKind.Chat, ct))
            return false;

        styleId = await ResolveStyleIdAsync(speaker, styleId, ct);

        IRoomPlayer? recipient = null;

        if (chatType == RoomChatType.Whisper)
        {
            recipient = FindPlayerAvatarByName(recipientName);

            if (recipient is null)
                return false;
        }

        var evt = new PlayerChatEvent
        {
            RoomId = _roomGrain._state.RoomId,
            CausedBy = ctx,
            PlayerId = ctx.PlayerId,
            ObjectId = speaker.ObjectId,
            ChatType = chatType,
            Text = text,
            StyleId = styleId,
            TrackingId = trackingId,
            Gesture = GetGestureForText(text),
            TargetPlayerId = recipient?.PlayerId,
        };

        await _roomGrain._eventSystem.PublishAsync(evt, ct);

        if (evt.IsCancelled)
            return true;

        // A line a "hide" User Says Keyword box picks up is not shown to the room: its speaker
        // alone gets it back, as a whisper over their own avatar (hhus, 2026-10-13: Chat and
        // Shout both came back as Whisper with the speaker as receiver). The wired still fires.
        if (chatType != RoomChatType.Whisper && await IsHiddenByWiredAsync(evt, ct))
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
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

        if (chatType != RoomChatType.Whisper)
            TurnHeadsTowards(speaker, chatType);

        await PetModule.HandleChatAsync(evt, ct);

        // Kept by the room and handed to persistence with the rest of what changed, rather than
        // a grain call per line.
        if (_roomGrain._roomConfig.ChatlogEnabled)
            _roomGrain.QueueChatlog(
                new RoomChatlogSnapshot
                {
                    RoomId = evt.RoomId,
                    PlayerId = evt.PlayerId,
                    TargetPlayerId = evt.TargetPlayerId,
                    Text = evt.Text,
                }
            );

        await _roomGrain.PublishRoomEventAsync(evt, ct);

        return true;
    }

    /// <summary>
    /// A bubble over an avatar that is not a player typing: a bot's line, a pet's reply, a wired
    /// message, the room telling a player why they were muted. The words are filtered like
    /// anything players read, but it is not chat: no flood check, no commands, no chat log, no
    /// event. Every such bubble is built here, so a change to what a bubble carries is one edit.
    /// </summary>
    public Task SayAsAvatarAsync(
        IRoomAvatar speaker,
        string text,
        AvatarSpeech speech,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        text = ModerationModule.ApplyFilter(text);

        if (speech.OnlyFor is { } listener)
            return _roomGrain._grainFactory.SendComposerToPlayerAsync(
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

        return _roomGrain.SendComposerToRoomAsync(
            CreateComposer(
                speech.Shout ? RoomChatType.Shout : RoomChatType.Chat,
                speaker.ObjectId,
                text,
                AvatarGestureType.None,
                speech.StyleId,
                NO_TRACKING_ID,
                receiverRoomIndex: null,
                speech.BubbleWidth,
                speech.HideUsername
            ),
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
        foreach (var item in _roomGrain._state.ItemsById.Values)
        {
            if (
                item.Logic is WiredTriggerHabboSaysKeyword trigger
                && await trigger.HidesAsync(evt, ct)
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
                await _roomGrain._grainFactory.SendComposerToPlayerAsync(
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
                await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                    recipient.PlayerId,
                    CreateComposer(evt, masked[0].Text),
                    ct
                );
        }

        await _roomGrain._grainFactory.SendComposerToPlayersAsync(targets, composer, ct);
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

    private void TurnHeadsTowards(IRoomPlayer speaker, RoomChatType chatType)
    {
        var range = _roomGrain._roomConfig.ChatLookAtRange;

        foreach (var avatar in AvatarModule.Avatars)
        {
            if (avatar.ObjectId == speaker.ObjectId || avatar.IsWalking)
                continue;

            if (avatar.X == speaker.X && avatar.Y == speaker.Y)
                continue;

            if (chatType == RoomChatType.Chat)
            {
                var distance = Math.Max(
                    Math.Abs(avatar.X - speaker.X),
                    Math.Abs(avatar.Y - speaker.Y)
                );

                if (distance > range)
                    continue;
            }

            var targetRotation = RotationExtensions.FromPoints(
                avatar.X,
                avatar.Y,
                speaker.X,
                speaker.Y
            );

            // AS3 AvatarVisualization.updateObject and AvatarImage.setDirection trust the
            // server's head direction. An automatic chat reaction, a person's or a pet's, may
            // look ahead or one octant either side of the body, never over the shoulder;
            // explicit directions are separate.
            if (
                targetRotation != avatar.Rotation
                && targetRotation != avatar.Rotation.Rotate(-1)
                && targetRotation != avatar.Rotation.Rotate(1)
            )
                continue;

            avatar.SetHeadRotation(targetRotation);
        }
    }

    private async Task<bool> IsMutedAsync(PlayerId playerId, CancellationToken ct)
    {
        var remainingSeconds = ModerationModule.GetRemainingMuteSeconds(playerId);

        if (remainingSeconds > 0)
        {
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
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

        if (check.Decision?.ExpiresAt is { } until && until > DateTime.UtcNow)
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                playerId,
                new RemainingMutePeriodMessageComposer
                {
                    SecondsRemaining = (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds),
                },
                ct
            );

        return true;
    }

    private int GetFloodMaxMessages()
    {
        var config = _roomGrain._roomConfig;

        return _roomGrain._state.RoomSnapshot.ChatProtection switch
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
        var config = _roomGrain._roomConfig;
        var now = _roomGrain.NowMs();

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

            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
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
            hasClub: speaker.HabboClubExpiresAt is { } expiresAt && expiresAt > DateTime.UtcNow,
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
