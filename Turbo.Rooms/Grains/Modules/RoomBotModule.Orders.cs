using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Badges;
using Turbo.Primitives.Bots;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms;
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
    // A bot says at most ten lines in five seconds; what it is told to say past that is not said
    // (official client capture 2026-10-25, LAB-W8 wlab9 action.23.L3b / L4: a bot line re-firing
    // its own Says Keyword stack stopped at 10 lines, 9 when its greeting came 3.6 s before; a
    // firing 2.1, 3.6 or 4.6 s after a burst flashed the stack but the bot said nothing 3/3, 5.7
    // and 7.2 s after it said 10 again 2/2; 12 Bot Talk boxes in one firing said 10). The window
    // lies between 4.6 and 5.7 s; 5 s and the count per placed bot are inferences.
    private const int BOT_FLOOD_MAX_LINES = 10;
    private const long BOT_FLOOD_WINDOW_MS = 5_000;

    private readonly Dictionary<RoomObjectId, Queue<long>> _recentLineAtMsByBotObjectId = [];

    /// <summary>
    /// Every bot in the room with this name, case ignored and nothing trimmed: a name with a
    /// space before or after it names no bot, and two bots with the same name are both named
    /// (official client capture 2026-10-24, bots "Frank" and "frank": "Frank", "frank" and
    /// "FRANK" made both talk 6/6, "frank " and " Frank" neither 4/4).
    /// </summary>
    public List<IRoomBot> GetBotsByName(string name) =>
        name.Length == 0
            ? []
            : [.. Bots.Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// A bot speaks or shouts to the whole room; <paramref name="hideName"/> leaves its name off
    /// the bubble. A bot that said ten lines in the last five seconds says nothing.
    /// </summary>
    public async Task<bool> TalkAsync(
        IRoomBot bot,
        string text,
        bool shout,
        int? bubbleWidth,
        bool hideName,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(text) || !TryTakeLine(bot))
            return false;

        await ChatSystem.SayAsAvatarAsync(
            bot,
            text,
            new AvatarSpeech
            {
                StyleId = Config.ChatStyleId,
                Shout = shout,
                BubbleWidth = bubbleWidth,
                HideUsername = hideName,
            },
            ct
        );

        return true;
    }

    private bool TryTakeLine(IRoomBot bot)
    {
        var now = NowMs();

        if (!_recentLineAtMsByBotObjectId.TryGetValue(bot.ObjectId, out var lines))
            _recentLineAtMsByBotObjectId[bot.ObjectId] = lines = new Queue<long>();

        while (lines.Count > 0 && now - lines.Peek() >= BOT_FLOOD_WINDOW_MS)
            lines.Dequeue();

        if (lines.Count >= BOT_FLOOD_MAX_LINES)
            return false;

        lines.Enqueue(now);

        return true;
    }

    /// <summary>A bot whispers to one player; only that player sees the bubble.</summary>
    public Task WhisperAsync(
        IRoomBot bot,
        IRoomPlayer player,
        string text,
        int? bubbleWidth,
        bool hideName,
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
                HideUsername = hideName,
            },
            ct
        );

    /// <summary>Sends a bot walking to a furni; its step onto it fires "bot reached furni".</summary>
    public async Task<bool> WalkToItemAsync(IRoomBot bot, IRoomFloorItem item, CancellationToken ct)
    {
        bot.FollowObjectId = -1;
        bot.TargetItemId = item.ObjectId;

        // Already on it: no step onto it, so "bot reached furni" does not fire.
        if (IsOnItem(bot, item))
        {
            bot.TargetItemId = -1;

            return true;
        }

        if (await WalkTowardItemAsync(bot, item, ct))
            return true;

        bot.TargetItemId = -1;

        return false;
    }

    /// <summary>Whether the bot stands on any tile the furni covers.</summary>
    public bool IsOnItem(IRoomBot bot, IRoomFloorItem item) =>
        MapModule.TryGetTileIds(FloorFootprint.Of(item), out var tileIds)
        && tileIds.Contains(MapModule.ToIdx(bot.X, bot.Y));

    /// <summary>
    /// Walks a bot onto a free tile of a furni, a random one of those it can reach (two bots
    /// sent to a 2x2 furni each took a free tile of it). When every tile it could stand on is
    /// taken by an avatar the bot walks next to it instead (a taken chair: the second bot stopped
    /// on the tile beside it); a furni nobody can stand on, or one there is no way to, moves no
    /// bot (official client capture 2026-10-24, Bot Move onto a 2x2 furni 4/4, a chair, a lamp
    /// and a shelf out of reach 1/1 each). Which tile beside a taken one is inferred: the
    /// nearest to the bot.
    /// </summary>
    internal async Task<bool> WalkTowardItemAsync(
        IRoomBot bot,
        IRoomFloorItem item,
        CancellationToken ct
    )
    {
        var map = MapModule;

        if (!map.TryGetTileIds(FloorFootprint.Of(item), out var tileIds))
            return false;

        var free = tileIds.Where(x => map.CanAvatarWalk(bot, x)).ToList();

        while (free.Count > 0)
        {
            var index = NextRandom(0, free.Count);
            var (x, y) = map.GetTileXY(free[index]);

            if (await AvatarModule.WalkAvatarToAsync(bot, x, y, ct))
                return true;

            free.RemoveAt(index);
        }

        var botIdx = map.ToIdx(bot.X, bot.Y);
        var beside = tileIds
            .Where(x => map.CanAvatarWalk(bot, x, ignoreAvatars: true))
            .SelectMany(Neighbours)
            .Where(x => !tileIds.Contains(x) && map.CanAvatarWalk(bot, x))
            .Distinct()
            .OrderBy(x => map.GetDistanceBetween(botIdx, x));

        foreach (var tileIdx in beside)
        {
            if (tileIdx == botIdx)
                return false;

            var (x, y) = map.GetTileXY(tileIdx);

            if (await AvatarModule.WalkAvatarToAsync(bot, x, y, ct))
                return true;
        }

        return false;

        IEnumerable<int> Neighbours(int tileIdx)
        {
            for (var direction = 0; direction < 8; direction++)
                if (map.TryGetTileInFront(tileIdx, (Rotation)direction, out var next))
                    yield return next;
        }
    }

    /// <summary>
    /// Places a bot on a furni's tile without walking, whatever stands there: a user on the
    /// tile, a furni nobody can stand on, a chair it then sits on (official client capture
    /// 2026-10-24, Bot Teleport onto a user's tile, a lamp and a chair, 2/2 each).
    /// </summary>
    public async Task<bool> TeleportToItemAsync(
        IRoomBot bot,
        IRoomFloorItem item,
        CancellationToken ct
    )
    {
        var tileIdx = MapModule.ToIdx(item.X, item.Y);

        if (!MapModule.InBounds(tileIdx))
            return false;

        await AvatarModule.RelocateAvatarAsync(bot, tileIdx, ct);
        Persist(bot);

        return true;
    }

    /// <summary>
    /// A bot takes a hand item to an avatar: it carries the item at once, walks to a tile next
    /// to the avatar and hands it over there, the avatar taking the item and the bot's hand
    /// emptied in the same moment; a bot already next to the avatar, or with no way to it, hands
    /// it over at once (official client capture 2026-10-24: CarryObject bot, a walk, ~1.4 s later
    /// CarryObject avatar then CarryObject bot 0, 1/1; next to the user all three in one tick,
    /// 1/1; the user enclosed by furni, all three in the fire's tick, no walk, 1/1).
    /// </summary>
    public async Task GiveHandItemAsync(
        IRoomBot bot,
        IRoomAvatar avatar,
        int handItemId,
        CancellationToken ct
    )
    {
        await AvatarModule.SetHandItemAsync(bot, handItemId, ct);

        bot.HandOverObjectId = avatar.ObjectId;

        await ContinueHandOverAsync(bot, walked: false, ct);
    }

    /// <summary>
    /// Hands the carried item over when the bot is next to its avatar or no way leads to it,
    /// otherwise walks it there; the order ends once the item is handed over or the avatar has
    /// left. A bot that <paramref name="walked"/> there reaches the avatar as it hands the item
    /// over: Bot Reached Avatar fires in that tick (official client capture 2026-10-24T18-02-52,
    /// 1/1). That a way closed mid-walk hands it over at once too is inferred.
    /// </summary>
    internal async Task ContinueHandOverAsync(IRoomBot bot, bool walked, CancellationToken ct)
    {
        if (!AvatarModule.TryGetAvatar(bot.HandOverObjectId, out var avatar) || bot.HandItemId <= 0)
        {
            bot.HandOverObjectId = -1;

            return;
        }

        var map = MapModule;
        var avatarIdx = map.ToIdx(avatar.X, avatar.Y);
        var beside = map.GetDistanceBetween(map.ToIdx(bot.X, bot.Y), avatarIdx) <= 1;

        if (!beside && await WalkBesideAsync(bot, avatarIdx, ct))
            return;

        var handItemId = bot.HandItemId;

        bot.HandOverObjectId = -1;

        await AvatarModule.SetHandItemAsync(avatar, handItemId, ct);
        await AvatarModule.SetHandItemAsync(bot, 0, ct);

        if (beside && walked)
            await BotTickSystem.NotifyAvatarReachedAsync(bot, avatar, ct);
    }

    /// <summary>Walks a bot to a free tile beside a tile, trying the four sides in turn.</summary>
    internal async Task<bool> WalkBesideAsync(IRoomBot bot, int targetIdx, CancellationToken ct)
    {
        var map = MapModule;
        var botIdx = map.ToIdx(bot.X, bot.Y);

        foreach (var direction in RotationExtensions.CARDINAL)
        {
            if (!map.TryGetTileInFront(targetIdx, direction, out var nextIdx))
                continue;

            if (nextIdx != botIdx && !PetModule.IsTileFreeForNpc(nextIdx))
                continue;

            var (x, y) = map.GetTileXY(nextIdx);

            if (await AvatarModule.WalkAvatarToAsync(bot, x, y, ct))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Starts or stops a bot trailing an avatar; the tick keeps it one step behind. A bot that
    /// already stands within reach has not reached the avatar by being told to follow: Bot
    /// Reached Avatar fired neither at once nor while the user's walks kept the bot beside him,
    /// and fired once he had walked out of reach and the bot had caught up (official client
    /// capture 2026-10-25T01-13-38, wired matrix trigger.14.X1, 0/2 and 1/1; a follow started 6
    /// tiles away fired on arrival, 1/1).
    /// </summary>
    public Task<bool> FollowAsync(IRoomBot bot, RoomObjectId avatarObjectId, bool start)
    {
        bot.TargetItemId = -1;
        bot.FollowObjectId = start ? avatarObjectId : -1;

        if (start && AvatarModule.TryGetAvatar(avatarObjectId, out var avatar))
        {
            var distance = MapModule.GetDistanceBetween(
                MapModule.ToIdx(bot.X, bot.Y),
                MapModule.ToIdx(avatar.X, avatar.Y)
            );

            bot.ReachedObjectId = distance <= Config.FollowDistance ? avatarObjectId : -1;
        }

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

    /// <summary>
    /// Carries out what the hotel told a placed bot to do (<see cref="BotOrder"/>), as a wired bot
    /// action would: a line goes through the flood limit and the room's word filter, a walk
    /// ends any follow or furni the bot was heading for. False when the bot is not here or can't.
    /// </summary>
    public async Task<bool> OrderAsync(int botId, BotOrder order, CancellationToken ct)
    {
        if (!TryGetBot(botId, out var bot))
            return false;

        switch (order)
        {
            case BotSayOrder say:
                return await TalkAsync(bot, say.Text, say.Shout, null, false, ct);
            case BotWhisperOrder whisper:
                if (
                    string.IsNullOrWhiteSpace(whisper.Text)
                    || !AvatarModule.TryGetPlayer(whisper.Player, out var listener)
                )
                    return false;

                await WhisperAsync(bot, listener, whisper.Text, null, false, ct);

                return true;
            case BotWalkOrder walk:
                bot.FollowObjectId = -1;
                bot.TargetItemId = -1;

                return await AvatarModule.WalkAvatarToAsync(bot, walk.X, walk.Y, ct);
            case BotWalkToItemOrder toItem:
                return State.ItemsById.TryGetValue(new RoomObjectId(toItem.ItemId), out var item)
                    && item is IRoomFloorItem { IsInvisible: false } floorItem
                    && await WalkToItemAsync(bot, floorItem, ct);
            case BotGestureOrder
            {
                Gesture: AvatarExpressionType.Wave
                    or AvatarExpressionType.Blow
                    or AvatarExpressionType.Laugh
                    or AvatarExpressionType.Cry
                    or AvatarExpressionType.Jump,
            } gesture:
                return await AvatarModule.SetAvatarExpressionAsync(
                    bot.ObjectId,
                    gesture.Gesture,
                    ct
                );
            case BotDanceOrder dance when Enum.IsDefined(dance.Dance):
                return await AvatarModule.SetAvatarDanceAsync(bot.ObjectId, dance.Dance, ct);
            case BotSignOrder { Sign: >= 0 and <= 17 } sign:
                return await AvatarModule.SetAvatarSignAsync(bot.ObjectId, sign.Sign, ct);
            default:
                return false;
        }
    }
}
