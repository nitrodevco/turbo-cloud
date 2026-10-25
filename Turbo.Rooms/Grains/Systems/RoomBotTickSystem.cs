using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Events.Bot;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Bots roam when told to and recite their chat lines on the configured delay. Orders
/// (follow an avatar, walk to a furni) take precedence over free roaming and raise the bot
/// arrival triggers when they complete.
/// </summary>
public sealed class RoomBotTickSystem(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    // The loop walks a copy, kept rather than made again every boundary, as the pet tick does.
    private readonly List<IRoomBot> _bots = [];

    private BotConfig Config => _roomGrain._botConfig;

    public async Task ProcessBotsAsync(long now, CancellationToken ct)
    {
        _bots.Clear();
        _bots.AddRange(BotModule.Bots);

        foreach (var bot in _bots)
        {
            try
            {
                await ProcessBotAsync(bot, now, ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "Bot {BotId} in room {RoomId} failed to tick",
                    bot.BotId,
                    _roomGrain.RoomId
                );
            }
        }
    }

    /// <summary>
    /// Raises the "bot reached furni" trigger: a bot stepped onto a furni from a tile it does not
    /// cover. Habbo fires it for any bot's first step onto any tile of the furni, walking there on
    /// a Bot Move order or not, and for a Bot Teleport onto it; further steps on the furni do not
    /// fire it again (official client capture 2026-10-24, W8 lab trigger.13.R1 2 fires per run
    /// 2/2, R1b teleport, R4 after a reload 2/2).
    /// </summary>
    public Task NotifyItemReachedAsync(IRoomBot bot, IRoomFloorItem item, CancellationToken ct) =>
        _roomGrain.PublishRoomEventAsync(
            new BotReachedItemEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                BotObjectId = bot.ObjectId,
                BotName = bot.Name,
                FurniId = item.ObjectId,
            },
            ct
        );

    /// <summary>Raises the "bot reached avatar" trigger: a bot arrived beside an avatar.</summary>
    public Task NotifyAvatarReachedAsync(IRoomBot bot, IRoomAvatar avatar, CancellationToken ct) =>
        _roomGrain.PublishRoomEventAsync(
            new BotReachedAvatarEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                BotObjectId = bot.ObjectId,
                BotName = bot.Name,
                TargetObjectId = avatar.ObjectId,
            },
            ct
        );

    private async Task ProcessBotAsync(IRoomBot bot, long now, CancellationToken ct)
    {
        var module = BotModule;

        if (bot.AutoChat && bot.ChatLines.Length > 0 && now >= bot.NextChatAtMs)
        {
            var delay = Math.Max(Config.ChatDelayMinSeconds, bot.ChatDelaySeconds);

            bot.NextChatAtMs = now + delay * 1000L;

            var index = bot.MixSentences
                ? module.NextRandom(0, bot.ChatLines.Length)
                : bot.NextChatLineIndex % bot.ChatLines.Length;

            bot.NextChatLineIndex = (index + 1) % bot.ChatLines.Length;

            await module.TalkAsync(bot, bot.ChatLines[index], ct);
        }

        // Where a bot came to rest is written with the room's next hand-over to persistence.
        if (bot.IsWalking)
            return;

        if (bot.HandOverObjectId > 0)
        {
            await module.ContinueHandOverAsync(bot, walked: true, ct);

            return;
        }

        if (bot.TargetItemId > 0)
        {
            await ProcessItemTargetAsync(bot, ct);

            return;
        }

        if (bot.FollowObjectId > 0)
        {
            await ProcessFollowAsync(bot, now, ct);

            return;
        }

        if (!bot.FreeRoam || now < bot.NextWalkAtMs)
            return;

        bot.NextWalkAtMs =
            now + module.NextRandom(Config.FreeRoamMinIntervalMs, Config.FreeRoamMaxIntervalMs);

        var map = MapModule;
        var range = Config.FreeRoamMaxDistance;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var x = bot.X + module.NextRandom(-range, range + 1);
            var y = bot.Y + module.NextRandom(-range, range + 1);

            if (!map.InBounds(x, y) || (x == bot.X && y == bot.Y))
                continue;

            if (!PetModule.IsTileFreeForNpc(map.ToIdx(x, y)))
                continue;

            if (await AvatarModule.WalkAvatarToAsync(bot, x, y, ct))
                return;
        }
    }

    /// <summary>A bot that stopped walking while sent to a furni either arrived or gave up.</summary>
    private async Task ProcessItemTargetAsync(IRoomBot bot, CancellationToken ct)
    {
        if (
            !FurniModule.TryGetItem(bot.TargetItemId, out var found)
            || found is not IRoomFloorItem item
        )
        {
            bot.TargetItemId = -1;

            return;
        }

        // The trigger fired with the step onto the furni; standing on it ends the order.
        if (BotModule.IsOnItem(bot, item))
        {
            bot.TargetItemId = -1;

            return;
        }

        if (!await BotModule.WalkTowardItemAsync(bot, item, ct))
            bot.TargetItemId = -1;
    }

    /// <summary>Keeps a following bot within reach of its avatar and reports each catch-up.</summary>
    private async Task ProcessFollowAsync(IRoomBot bot, long now, CancellationToken ct)
    {
        if (!AvatarModule.TryGetAvatar(bot.FollowObjectId, out var target))
        {
            bot.FollowObjectId = -1;

            return;
        }

        var map = MapModule;
        var botIdx = map.ToIdx(bot.X, bot.Y);
        var targetIdx = map.ToIdx(target.X, target.Y);
        var distance = map.GetDistanceBetween(botIdx, targetIdx);

        // Bot Reached Avatar fires once as the bot stops beside the avatar, and again only after it
        // has walked after it once more (official client capture 2026-10-24T18-08-27: one fire
        // 0.5 s after each of two arrivals, none in the 18 s the bot stood beside it between).
        if (distance <= _roomGrain._botConfig.FollowDistance)
        {
            if (bot.ReachedObjectId == target.ObjectId)
                return;

            bot.ReachedObjectId = target.ObjectId;

            await NotifyAvatarReachedAsync(bot, target, ct);

            return;
        }

        bot.ReachedObjectId = -1;

        if (!RoomAvatarModule.IsFollowDue(bot, targetIdx, now))
            return;

        AvatarModule.RecordFollow(
            bot,
            targetIdx,
            await BotModule.WalkBesideAsync(bot, targetIdx, ct),
            now
        );
    }
}
