using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Badges;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Orders a bot can be given by something other than its owner: talk, whisper, move, teleport,
/// follow, dress. Wired bot actions are the caller today; nothing here depends on that.
/// </summary>
public sealed partial class RoomBotModule
{
    /// <summary>The first bot in the room with this name, case-insensitive.</summary>
    public bool TryGetBotByName(string name, out IRoomBot bot)
    {
        bot = null!;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        var found = Bots.FirstOrDefault(x =>
            string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)
        );

        if (found is null)
            return false;

        bot = found;

        return true;
    }

    /// <summary>A bot speaks or shouts to the whole room.</summary>
    public Task TalkAsync(
        IRoomBot bot,
        string text,
        bool shout,
        int? bubbleWidth,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        text = _roomGrain.ModerationModule.ApplyFilter(text);

        ChatMessageComposer composer = shout
            ? new ShoutMessageComposer
            {
                ObjectId = bot.ObjectId,
                Text = text,
                Gesture = AvatarGestureType.None,
                StyleId = Config.ChatStyleId,
                Links = [],
                TrackingId = -1,
                ChatBubbleWidthOverride = bubbleWidth,
            }
            : new ChatMessageComposer
            {
                ObjectId = bot.ObjectId,
                Text = text,
                Gesture = AvatarGestureType.None,
                StyleId = Config.ChatStyleId,
                Links = [],
                TrackingId = -1,
                ChatBubbleWidthOverride = bubbleWidth,
            };

        return _roomGrain.SendComposerToRoomAsync(composer, ct);
    }

    /// <summary>A bot whispers to one player; only that player sees the bubble.</summary>
    public Task WhisperAsync(
        IRoomBot bot,
        IRoomPlayer player,
        string text,
        int? bubbleWidth,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        text = _roomGrain.ModerationModule.ApplyFilter(text);

        return _roomGrain._grainFactory.SendComposerToPlayerAsync(
            player.PlayerId,
            new WhisperMessageComposer
            {
                ObjectId = bot.ObjectId,
                Text = text,
                Gesture = AvatarGestureType.None,
                StyleId = Config.ChatStyleId,
                Links = [],
                TrackingId = -1,
                ReceiverRoomIndex = player.ObjectId,
                ChatBubbleWidthOverride = bubbleWidth,
            },
            ct
        );
    }

    /// <summary>Sends a bot walking to a furni; arrival raises the "bot reached furni" trigger.</summary>
    public async Task<bool> WalkToItemAsync(IRoomBot bot, IRoomFloorItem item, CancellationToken ct)
    {
        bot.FollowObjectId = -1;
        bot.TargetItemId = item.ObjectId;

        if (_roomGrain.MapModule.ToIdx(bot.X, bot.Y) == _roomGrain.MapModule.ToIdx(item.X, item.Y))
        {
            await _roomGrain.BotTickSystem.NotifyItemReachedAsync(bot, ct);

            return true;
        }

        return await _roomGrain.AvatarModule.WalkAvatarToAsync(bot, item.X, item.Y, ct);
    }

    /// <summary>Places a bot on a furni tile without walking.</summary>
    public async Task<bool> TeleportToItemAsync(
        IRoomBot bot,
        IRoomFloorItem item,
        CancellationToken ct
    )
    {
        var tileIdx = _roomGrain.MapModule.ToIdx(item.X, item.Y);

        if (!_roomGrain.PetModule.IsTileFreeForNpc(tileIdx) && !CanShareTile(bot, tileIdx))
            return false;

        await _roomGrain.AvatarModule.StopWalkingAsync(bot, ct);

        _roomGrain.MapModule.RemoveAvatar(bot, false);

        bot.SetPosition(item.X, item.Y);

        _roomGrain.MapModule.AddAvatar(bot, false);
        _roomGrain.MapModule.UpdateHeightForAvatar(bot);

        bot.NeedsInvoke = true;
        bot.MarkDirty();

        await PersistAsync(bot, ct);

        return true;
    }

    /// <summary>Starts or stops a bot trailing an avatar; the tick keeps it one step behind.</summary>
    public Task<bool> FollowAsync(IRoomBot bot, RoomObjectId avatarObjectId, bool start)
    {
        bot.TargetItemId = -1;
        bot.FollowObjectId = start ? avatarObjectId : -1;

        return Task.FromResult(true);
    }

    /// <summary>Dresses a bot in a figure string and tells the room.</summary>
    public async Task SetFigureAsync(
        IRoomBot bot,
        string figure,
        AvatarGenderType gender,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(figure))
            return;

        bot.SetFigure(figure, gender);

        await _roomGrain.SendComposerToRoomAsync(
            new UserChangeMessageComposer
            {
                ObjectId = bot.ObjectId,
                Figure = bot.Figure,
                Gender = bot.Gender,
                CustomInfo = bot.Motto,
                AchievementScore = 0,
                BadgesRank = BadgeRanks.NONE,
            },
            ct
        );

        await PersistAsync(bot, ct);
    }

    private bool CanShareTile(IRoomBot bot, int tileIdx) =>
        _roomGrain._state.TileAvatarStacks[tileIdx].Contains(bot.ObjectId);
}
