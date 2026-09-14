using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Chat;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Room chat pipeline. Every message becomes a <see cref="PlayerChatEvent"/> that is first offered
/// to the global event pipeline (chat commands, filters), then broadcast, then published to the
/// room event module so room listeners such as the wired system can react to it.
/// </summary>
public sealed class RoomChatSystem(RoomGrain roomGrain) : IRoomEventListener
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private readonly Dictionary<PlayerId, Queue<long>> _chatTimestampsByPlayerId = [];
    private readonly Dictionary<PlayerId, long> _floodMutedUntilByPlayerId = [];

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
        if (!TryGetPlayerAvatar(ctx.PlayerId, out var speaker))
            return false;

        text = NormalizeText(text);

        if (text.Length == 0)
            return false;

        if (await IsMutedAsync(ctx.PlayerId))
            return false;

        if (await IsFloodingAsync(ctx.PlayerId, ct))
            return false;

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

        await BroadcastAsync(evt, speaker, recipient);

        if (chatType != RoomChatType.Whisper)
            TurnHeadsTowards(speaker, chatType);

        if (_roomGrain._roomConfig.ChatlogEnabled)
            await _roomGrain
                ._grainFactory.GetRoomPersistenceGrain(_roomGrain._state.RoomId)
                .EnqueueChatlogAsync(
                    new RoomChatlogSnapshot
                    {
                        RoomId = evt.RoomId,
                        PlayerId = evt.PlayerId,
                        TargetPlayerId = evt.TargetPlayerId,
                        Text = evt.Text,
                    },
                    ct
                );

        await _roomGrain.PublishRoomEventAsync(evt, ct);

        return true;
    }

    public Task<bool> SetAvatarTypingAsync(ActionContext ctx, bool isTyping)
    {
        if (!TryGetPlayerAvatar(ctx.PlayerId, out var avatar))
            return Task.FromResult(false);

        _ = _roomGrain.SendComposerToRoomAsync(
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

    private async Task BroadcastAsync(
        PlayerChatEvent evt,
        IRoomPlayer speaker,
        IRoomPlayer? recipient
    )
    {
        var composer = CreateComposer(evt);

        if (evt.ChatType != RoomChatType.Whisper)
        {
            await _roomGrain.SendComposerToRoomAsync(composer);

            return;
        }

        var targets = new HashSet<PlayerId> { speaker.PlayerId };

        if (recipient is not null)
            targets.Add(recipient.PlayerId);

        await _roomGrain.SendComposerToPlayersAsync(targets, composer);
    }

    private static IComposer CreateComposer(PlayerChatEvent evt) =>
        evt.ChatType switch
        {
            RoomChatType.Shout => new ShoutMessageComposer
            {
                ObjectId = evt.ObjectId,
                Text = evt.Text,
                Gesture = evt.Gesture,
                StyleId = evt.StyleId,
                Links = [],
                TrackingId = evt.TrackingId,
            },
            RoomChatType.Whisper => new WhisperMessageComposer
            {
                ObjectId = evt.ObjectId,
                Text = evt.Text,
                Gesture = evt.Gesture,
                StyleId = evt.StyleId,
                Links = [],
                TrackingId = evt.TrackingId,
            },
            _ => new ChatMessageComposer
            {
                ObjectId = evt.ObjectId,
                Text = evt.Text,
                Gesture = evt.Gesture,
                StyleId = evt.StyleId,
                Links = [],
                TrackingId = evt.TrackingId,
            },
        };

    private void TurnHeadsTowards(IRoomPlayer speaker, RoomChatType chatType)
    {
        var range = _roomGrain._roomConfig.ChatLookAtRange;

        foreach (var avatar in _roomGrain._state.AvatarsByObjectId.Values)
        {
            if (avatar.ObjectId == speaker.ObjectId || avatar.IsWalking)
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

            avatar.SetHeadRotation(
                RotationExtensions.FromPoints(avatar.X, avatar.Y, speaker.X, speaker.Y)
            );
        }
    }

    private async Task<bool> IsMutedAsync(PlayerId playerId)
    {
        var remainingSeconds = _roomGrain.ModerationModule.GetRemainingMuteSeconds(playerId);

        if (remainingSeconds > 0)
        {
            await _roomGrain
                ._grainFactory.GetPlayerPresenceGrain(playerId)
                .SendComposerAsync(
                    new RemainingMutePeriodMessageComposer { SecondsRemaining = remainingSeconds }
                );

            return true;
        }

        return await _roomGrain.ModerationModule.IsSilencedByRoomMuteAsync(playerId);
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

    private async Task<bool> IsFloodingAsync(PlayerId playerId, CancellationToken ct)
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
            _floodMutedUntilByPlayerId[playerId] = now + config.ChatFloodMuteMs;
            timestamps.Clear();

            await _roomGrain
                ._grainFactory.GetPlayerPresenceGrain(playerId)
                .SendComposerAsync(
                    new FloodControlMessageComposer
                    {
                        Seconds = (int)Math.Ceiling(config.ChatFloodMuteMs / 1000d),
                    }
                );

            return true;
        }

        timestamps.Enqueue(now);

        return false;
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Trim();

        var maxLength = _roomGrain._roomConfig.ChatMaxLength;

        return maxLength > 0 && text.Length > maxLength ? text[..maxLength] : text;
    }

    private bool TryGetPlayerAvatar(PlayerId playerId, out IRoomPlayer player)
    {
        player = null!;

        if (
            playerId <= 0
            || !_roomGrain._state.AvatarsByPlayerId.TryGetValue(playerId, out var objectId)
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
            || avatar is not IRoomPlayer roomPlayer
        )
            return false;

        player = roomPlayer;

        return true;
    }

    private IRoomPlayer? FindPlayerAvatarByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _roomGrain
            ._state.AvatarsByObjectId.Values.OfType<IRoomPlayer>()
            .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
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
