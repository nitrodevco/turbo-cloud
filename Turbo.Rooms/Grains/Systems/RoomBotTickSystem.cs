using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Events.Bot;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Bots roam when told to and recite their chat lines on the configured delay. Orders
/// (follow an avatar, walk to a furni) take precedence over free roaming and raise the bot
/// arrival triggers when they complete.
/// </summary>
public sealed class RoomBotTickSystem(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private BotConfig Config => _roomGrain._botConfig;

    public async Task ProcessBotsAsync(long now, CancellationToken ct)
    {
        foreach (var bot in _roomGrain.BotModule.Bots.ToList())
        {
            try
            {
                await ProcessBotAsync(bot, now, ct);
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogError(
                    ex,
                    "Bot {BotId} in room {RoomId} failed to tick",
                    bot.BotId,
                    _roomGrain.RoomId
                );
            }
        }
    }

    /// <summary>Raises the "bot reached furni" trigger for the item the bot was sent to.</summary>
    public async Task NotifyItemReachedAsync(IRoomBot bot, CancellationToken ct)
    {
        var itemId = bot.TargetItemId;

        bot.TargetItemId = -1;

        if (itemId <= 0 || !_roomGrain._state.ItemsById.ContainsKey(itemId))
            return;

        await _roomGrain.PublishRoomEventAsync(
            new BotReachedItemEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                BotObjectId = bot.ObjectId,
                BotName = bot.Name,
                FurniId = itemId,
            },
            ct
        );
    }

    private async Task ProcessBotAsync(IRoomBot bot, long now, CancellationToken ct)
    {
        var module = _roomGrain.BotModule;

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

        if (bot.IsWalking)
            return;

        await module.PersistPositionIfMovedAsync(bot, ct);

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

        var map = _roomGrain.MapModule;
        var range = Config.FreeRoamMaxDistance;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var x = bot.X + module.NextRandom(-range, range + 1);
            var y = bot.Y + module.NextRandom(-range, range + 1);

            if (!map.InBounds(x, y) || (x == bot.X && y == bot.Y))
                continue;

            if (!_roomGrain.PetModule.IsTileFreeForNpc(map.ToIdx(x, y)))
                continue;

            if (await _roomGrain.AvatarModule.WalkAvatarToAsync(bot, x, y, ct))
                return;
        }
    }

    /// <summary>A bot that stopped walking while sent to a furni either arrived or gave up.</summary>
    private async Task ProcessItemTargetAsync(IRoomBot bot, CancellationToken ct)
    {
        if (!_roomGrain._state.ItemsById.TryGetValue(bot.TargetItemId, out var item))
        {
            bot.TargetItemId = -1;

            return;
        }

        var map = _roomGrain.MapModule;

        if (map.ToIdx(bot.X, bot.Y) == map.ToIdx(item.X, item.Y))
        {
            await NotifyItemReachedAsync(bot, ct);

            return;
        }

        if (!await _roomGrain.AvatarModule.WalkAvatarToAsync(bot, item.X, item.Y, ct))
            bot.TargetItemId = -1;
    }

    /// <summary>Keeps a following bot within reach of its avatar and reports each catch-up.</summary>
    private async Task ProcessFollowAsync(IRoomBot bot, long now, CancellationToken ct)
    {
        if (!_roomGrain._state.AvatarsByObjectId.TryGetValue(bot.FollowObjectId, out var target))
        {
            bot.FollowObjectId = -1;

            return;
        }

        var map = _roomGrain.MapModule;
        var botIdx = map.ToIdx(bot.X, bot.Y);
        var targetIdx = map.ToIdx(target.X, target.Y);
        var distance = map.GetDistanceBetween(botIdx, targetIdx);

        if (distance <= _roomGrain._botConfig.FollowDistance)
        {
            if (now < bot.NextWalkAtMs)
                return;

            bot.NextWalkAtMs = now + Config.FreeRoamMinIntervalMs;

            await _roomGrain.PublishRoomEventAsync(
                new BotReachedAvatarEvent
                {
                    RoomId = _roomGrain.RoomId,
                    CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                    BotObjectId = bot.ObjectId,
                    BotName = bot.Name,
                    TargetObjectId = target.ObjectId,
                },
                ct
            );

            return;
        }

        foreach (var direction in Primitives.Rooms.Enums.RotationExtensions.CARDINAL)
        {
            if (!map.TryGetTileInFront(targetIdx, direction, out var nextIdx))
                continue;

            if (nextIdx != botIdx && !_roomGrain.PetModule.IsTileFreeForNpc(nextIdx))
                continue;

            var (x, y) = map.GetTileXY(nextIdx);

            if (await _roomGrain.AvatarModule.WalkAvatarToAsync(bot, x, y, ct))
                return;
        }
    }
}
