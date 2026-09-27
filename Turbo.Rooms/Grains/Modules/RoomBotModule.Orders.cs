using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Badges;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Orders a bot can be given by something other than its owner: talk, whisper, move, teleport,
/// follow, dress. Wired bot actions are the caller today; nothing here depends on that.
/// </summary>
public sealed partial class RoomBotModule
{
    /// <summary>The first bot in the room with this name, case-insensitive.</summary>
    public bool TryGetBotByName(string name, [NotNullWhen(true)] out IRoomBot? bot)
    {
        bot = null;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        bot = Bots.FirstOrDefault(x =>
            string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)
        );

        return bot is not null;
    }

    /// <summary>A bot speaks or shouts to the whole room.</summary>
    public Task TalkAsync(
        IRoomBot bot,
        string text,
        bool shout,
        int? bubbleWidth,
        CancellationToken ct
    ) =>
        ChatSystem.SayAsAvatarAsync(
            bot,
            text,
            new AvatarSpeech
            {
                StyleId = Config.ChatStyleId,
                Shout = shout,
                BubbleWidth = bubbleWidth,
            },
            ct
        );

    /// <summary>A bot whispers to one player; only that player sees the bubble.</summary>
    public Task WhisperAsync(
        IRoomBot bot,
        IRoomPlayer player,
        string text,
        int? bubbleWidth,
        CancellationToken ct
    ) =>
        ChatSystem.SayAsAvatarAsync(
            bot,
            text,
            new AvatarSpeech
            {
                StyleId = Config.ChatStyleId,
                OnlyFor = player,
                BubbleWidth = bubbleWidth,
            },
            ct
        );

    /// <summary>Sends a bot walking to a furni; arrival raises the "bot reached furni" trigger.</summary>
    public async Task<bool> WalkToItemAsync(IRoomBot bot, IRoomFloorItem item, CancellationToken ct)
    {
        bot.FollowObjectId = -1;
        bot.TargetItemId = item.ObjectId;

        if (MapModule.ToIdx(bot.X, bot.Y) == MapModule.ToIdx(item.X, item.Y))
        {
            await BotTickSystem.NotifyItemReachedAsync(bot, ct);

            return true;
        }

        return await AvatarModule.WalkAvatarToAsync(bot, item.X, item.Y, ct);
    }

    /// <summary>Places a bot on a furni tile without walking.</summary>
    public async Task<bool> TeleportToItemAsync(
        IRoomBot bot,
        IRoomFloorItem item,
        CancellationToken ct
    )
    {
        var tileIdx = MapModule.ToIdx(item.X, item.Y);

        if (!PetModule.IsTileFreeForNpc(tileIdx) && !CanShareTile(bot, tileIdx))
            return false;

        await AvatarModule.RelocateAvatarAsync(bot, tileIdx, ct);
        Persist(bot);

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

        Persist(bot);
    }

    private bool CanShareTile(IRoomBot bot, int tileIdx) =>
        _roomGrain._state.TileAvatarStacks[tileIdx].Contains(bot.ObjectId);
}
