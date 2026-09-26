using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Bots;
using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Bots;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Texts;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Rentable bots standing in the room: placement and pick-up against the owner's inventory,
/// and the skills their owner uses on them. Roaming and chatter run in
/// <see cref="Systems.RoomBotTickSystem"/>.
/// </summary>
public sealed partial class RoomBotModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly Dictionary<int, int> _lastPersistedTileByBotId = [];
    private readonly Random _random = new();

    private BotConfig Config => _roomGrain._botConfig;

    public IEnumerable<IRoomBot> Bots => _roomGrain.AvatarModule.Avatars.OfType<IRoomBot>();

    internal async Task EnsureBotsLoadedAsync(CancellationToken ct)
    {
        if (_roomGrain._state.IsBotsLoaded)
            return;

        var bots = await _roomGrain._npcProvider.LoadBotsByRoomIdAsync(_roomGrain.RoomId, ct);

        foreach (var bot in bots)
        {
            var tileIdx = _roomGrain.MapModule.InBounds(bot.X, bot.Y)
                ? _roomGrain.MapModule.ToIdx(bot.X, bot.Y)
                : -1;

            if (tileIdx < 0 && !_roomGrain.PetModule.TryFindFreeTile(0, 0, out tileIdx))
            {
                _roomGrain._logger.LogWarning(
                    "Bot {BotId} has no tile to stand on in room {RoomId}; leaving it unplaced",
                    bot.Id,
                    _roomGrain.RoomId
                );

                continue;
            }

            await AttachBotAsync(bot, tileIdx, bot.Rotation, ct);
        }

        _roomGrain._state.IsBotsLoaded = true;
    }

    public bool TryGetBot(int botId, out IRoomBot bot)
    {
        bot = null!;

        if (
            !_roomGrain._state.AvatarsByBotId.TryGetValue(botId, out var objectId)
            || !_roomGrain.AvatarModule.TryGetAvatar(objectId, out var avatar)
            || avatar is not IRoomBot roomBot
        )
            return false;

        bot = roomBot;

        return true;
    }

    private Task SendErrorAsync(ActionContext ctx, BotErrorType error, CancellationToken ct) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new BotErrorMessageComposer { Error = error },
            ct
        );

    /// <summary>The bot's owner or any room controller may move or pick it up.</summary>
    private async Task<bool> CanManageAsync(ActionContext ctx, IRoomBot bot) =>
        bot.OwnerId == ctx.PlayerId
        || await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx)
            >= RoomControllerType.Rights;

    public async Task<bool> PlaceBotAsync(
        ActionContext ctx,
        int botId,
        int x,
        int y,
        CancellationToken ct
    )
    {
        if (!_roomGrain.AvatarModule.TryGetPlayer(ctx.PlayerId, out _))
            return false;

        // Only the room owner places a bot, whatever the room's settings. Pets have a rule of
        // their own (RoomPetModule.PlacePetAsync); the two differ on purpose.
        if (!await _roomGrain.SecurityModule.GetIsRoomOwnerAsync(ctx))
        {
            await SendErrorAsync(ctx, BotErrorType.ForbiddenInFlat, ct);

            return false;
        }

        if (Bots.Count() >= Config.MaxBotsPerRoom)
        {
            await SendErrorAsync(ctx, BotErrorType.LimitReached, ct);

            return false;
        }

        var placed = await _roomGrain.PetModule.PlaceFromInventoryAsync(
            ctx,
            "bot",
            botId,
            x,
            y,
            () =>
                _roomGrain
                    ._grainFactory.GetInventoryGrain(ctx.PlayerId)
                    .TryCheckOutBotAsync(botId, _roomGrain.RoomId, ct),
            snapshot =>
                _roomGrain
                    ._grainFactory.GetInventoryGrain(ctx.PlayerId)
                    .ReturnBotAsync(snapshot, ct),
            (snapshot, tileIdx, z) =>
            {
                var (tileX, tileY) = _roomGrain.MapModule.GetTileXY(tileIdx);
                var standing = snapshot with
                {
                    RoomId = _roomGrain.RoomId,
                    X = tileX,
                    Y = tileY,
                    Z = z,
                };

                return AttachBotAsync(standing, tileIdx, standing.Rotation, ct);
            },
            // The bot error table has one tile error, so "no free tile at all" says it too.
            _ => SendErrorAsync(ctx, BotErrorType.SelectedTileNotFree, ct)
        );

        if (!placed)
            return false;

        if (TryGetBot(botId, out var bot))
            await PersistAsync(bot, ct);

        // A bot that was just placed is about to be set up, so the client is asked to open its
        // menu. Told, not awaited: the presence may be waiting on this room.
        _roomGrain
            ._grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new BotForceOpenContextMenuMessageComposer { BotId = botId },
                CancellationToken.None
            )
            .LogAndForget(
                _roomGrain._logger,
                $"open the menu of bot {botId} for player {ctx.PlayerId}"
            );

        return true;
    }

    public async Task<bool> MoveBotAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int x,
        int y,
        Rotation rotation,
        CancellationToken ct
    )
    {
        if (
            !_roomGrain.AvatarModule.TryGetAvatar(objectId, out var avatar)
            || avatar is not IRoomBot bot
        )
            return false;

        if (!await CanManageAsync(ctx, bot) || !_roomGrain.MapModule.InBounds(x, y))
            return false;

        var tileIdx = _roomGrain.MapModule.ToIdx(x, y);

        if (tileIdx != _roomGrain.MapModule.ToIdx(bot.X, bot.Y))
        {
            if (!_roomGrain.PetModule.IsTileFreeForNpc(tileIdx))
                return false;

            await _roomGrain.AvatarModule.RelocateAvatarAsync(bot, tileIdx, ct);
        }

        if (rotation != Rotation.None)
            bot.SetRotation(rotation);

        bot.MarkDirty();

        await PersistAsync(bot, ct);

        return true;
    }

    public async Task<bool> PickupBotAsync(ActionContext ctx, int botId, CancellationToken ct)
    {
        if (!TryGetBot(botId, out var bot))
            return false;

        if (!await CanManageAsync(ctx, bot))
            return false;

        await _roomGrain.ObjectModule.RemoveObjectAsync(ctx, bot, ct);

        _roomGrain._state.AvatarsByBotId.Remove(botId);
        _lastPersistedTileByBotId.Remove(botId);

        if (
            !await _roomGrain
                ._grainFactory.GetInventoryGrain(bot.OwnerId)
                .ReturnBotAsync(bot.GetBotSnapshot(), ct)
        )
        {
            _roomGrain._logger.LogError(
                "Bot {BotId} picked up from room {RoomId} could not be returned to player {OwnerId}",
                botId,
                _roomGrain.RoomId,
                bot.OwnerId
            );
        }

        return true;
    }

    public async Task<bool> CommandBotAsync(
        ActionContext ctx,
        int botId,
        BotSkillType skill,
        string data,
        CancellationToken ct
    )
    {
        if (!TryGetBot(botId, out var bot) || bot.OwnerId != ctx.PlayerId)
            return false;

        if (!bot.Skills.Contains(skill))
            return false;

        switch (skill)
        {
            case BotSkillType.DressUp:
                return await DressUpAsync(ctx, bot, ct);
            case BotSkillType.SetupChat:
                return await SetupChatAsync(ctx, bot, data, ct);
            case BotSkillType.RandomWalk:
                bot.SetFreeRoam(!bot.FreeRoam);

                if (!bot.FreeRoam)
                    await _roomGrain.AvatarModule.StopWalkingAsync(bot, ct);

                await PersistAsync(bot, ct);

                return true;
            case BotSkillType.Dance:
                return await ToggleDanceAsync(bot, ct);
            case BotSkillType.ChangeName:
                return await RenameAsync(ctx, bot, data, ct);
            default:
                _roomGrain._logger.LogWarning(
                    "Bot skill {Skill} on bot {BotId} in room {RoomId} has no behaviour",
                    skill,
                    botId,
                    _roomGrain.RoomId
                );

                return false;
        }
    }

    public async Task<bool> RequestConfigurationAsync(
        ActionContext ctx,
        int botId,
        BotSkillType skill,
        CancellationToken ct
    )
    {
        if (!TryGetBot(botId, out var bot) || bot.OwnerId != ctx.PlayerId)
            return false;

        string data;

        switch (skill)
        {
            case BotSkillType.SetupChat:
                data = BotChatterConfig.Compose(
                    bot.ChatText,
                    bot.AutoChat,
                    bot.ChatDelaySeconds,
                    bot.MixSentences
                );
                break;
            case BotSkillType.ChangeName:
                data = bot.Name;
                break;
            default:
                return false;
        }

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new BotCommandConfigurationMessageComposer
            {
                BotId = botId,
                Skill = skill,
                Data = data,
            },
            ct
        );

        return true;
    }

    private async Task<bool> DressUpAsync(ActionContext ctx, IRoomBot bot, CancellationToken ct)
    {
        var owner = await _roomGrain._grainFactory.GetPlayerGrain(ctx.PlayerId).GetSummaryAsync(ct);

        await SetFigureAsync(bot, owner.Figure, owner.Gender, ct);

        return true;
    }

    private async Task<bool> SetupChatAsync(
        ActionContext ctx,
        IRoomBot bot,
        string data,
        CancellationToken ct
    )
    {
        if (
            !BotChatterConfig.TryParse(
                data,
                out var text,
                out var autoChat,
                out var delay,
                out var mix
            )
        )
        {
            _roomGrain._logger.LogWarning(
                "Player {PlayerId} sent an unreadable chat setup for bot {BotId} in room {RoomId}",
                ctx.PlayerId,
                bot.BotId,
                _roomGrain.RoomId
            );

            return false;
        }

        text = ClientText.Truncate(text, Config.ChatTextMaxLength);

        var lines = BotChatLines.Split(text);

        if (lines.Length > Config.MaxChatLines)
            text = string.Join('\n', lines.Take(Config.MaxChatLines));

        delay = Math.Clamp(delay, Config.ChatDelayMinSeconds, Config.ChatDelayMaxSeconds);

        bot.SetChatter(text, autoChat, delay, mix);
        bot.NextChatAtMs = _roomGrain.NowMs() + delay * 1000L;

        await PersistAsync(bot, ct);

        return true;
    }

    private async Task<bool> ToggleDanceAsync(IRoomBot bot, CancellationToken ct)
    {
        var next =
            bot.DanceType == AvatarDanceType.None ? AvatarDanceType.Dance : AvatarDanceType.None;

        // The shared avatar path sets the dance and tells the room, the same one a dancing player
        // goes through. A bot's dance is also part of how it was left configured, so it persists.
        if (!await _roomGrain.AvatarModule.SetAvatarDanceAsync(bot.ObjectId, next, ct))
            return false;

        await PersistAsync(bot, ct);

        return true;
    }

    /// <summary>A renamed bot is re-sent to the room, as the Users packet is the only carrier of a name.</summary>
    private async Task<bool> RenameAsync(
        ActionContext ctx,
        IRoomBot bot,
        string name,
        CancellationToken ct
    )
    {
        var status = PetNames.Validate(name, Config.NameMinLength, Config.NameMaxLength);

        if (status != PetNameValidationType.Ok)
        {
            await SendErrorAsync(ctx, BotErrorType.NameNotAccepted, ct);

            return false;
        }

        bot.SetName(name.Trim());

        await _roomGrain.SendComposerToRoomAsync(
            new UserRemoveMessageComposer { ObjectId = bot.ObjectId },
            ct
        );
        await _roomGrain.SendComposerToRoomAsync(
            new UsersMessageComposer { Avatars = [bot.GetSnapshot()] },
            ct
        );
        await PersistAsync(bot, ct);

        return true;
    }

    internal Task TalkAsync(IRoomBot bot, string text, CancellationToken ct) =>
        TalkAsync(bot, text, shout: false, bubbleWidth: null, ct);

    private async Task<bool> AttachBotAsync(
        BotSnapshot snapshot,
        int tileIdx,
        Rotation rotation,
        CancellationToken ct
    )
    {
        var objectId = _roomGrain.AvatarModule.GetNextObjectId();
        var bot = _roomGrain._avatarProvider.CreateAvatarFromBotSnapshot(objectId, snapshot);

        bot.NextTileId = tileIdx;

        if (!await _roomGrain.ObjectModule.AttatchObjectAsync(bot, ct))
            return false;

        bot.SetRotation(rotation == Rotation.None ? Rotation.South : rotation);

        _roomGrain._state.AvatarsByBotId[bot.BotId] = bot.ObjectId;
        _lastPersistedTileByBotId[bot.BotId] = tileIdx;

        if (bot.DanceType != AvatarDanceType.None)
            await _roomGrain.SendComposerToRoomAsync(
                new DanceMessageComposer { ObjectId = bot.ObjectId, DanceType = bot.DanceType },
                ct
            );

        if (bot.EffectId > 0)
            await _roomGrain.SendComposerToRoomAsync(
                new AvatarEffectMessageComposer
                {
                    ObjectId = bot.ObjectId,
                    EffectId = bot.EffectId,
                    DelayMilliseconds = 0,
                },
                ct
            );

        return true;
    }

    /// <summary>
    /// Returns every bot to its owner's inventory, as a room deletion requires. The room lets go
    /// of them all first; the owners' inventories are separate grains and take theirs back side
    /// by side.
    /// </summary>
    internal async Task ReturnAllToOwnersAsync(CancellationToken ct)
    {
        var bots = Bots.ToList();

        foreach (var bot in bots)
        {
            await _roomGrain.ObjectModule.RemoveObjectAsync(
                ActionContext.CreateForSystem(_roomGrain.RoomId),
                bot,
                ct
            );

            _roomGrain._state.AvatarsByBotId.Remove(bot.BotId);
            _lastPersistedTileByBotId.Remove(bot.BotId);
        }

        await Task.WhenAll(
            bots.GroupBy(x => x.OwnerId).Select(owned => ReturnToOwnerAsync(owned.Key, owned, ct))
        );
    }

    private async Task ReturnToOwnerAsync(
        PlayerId ownerId,
        IEnumerable<IRoomBot> bots,
        CancellationToken ct
    )
    {
        var inventory = _roomGrain._grainFactory.GetInventoryGrain(ownerId);

        foreach (var bot in bots)
        {
            if (!await inventory.ReturnBotAsync(bot.GetBotSnapshot(), ct))
                _roomGrain._logger.LogError(
                    "Bot {BotId} could not be returned to player {OwnerId} while room {RoomId} is deleted",
                    bot.BotId,
                    ownerId,
                    _roomGrain.RoomId
                );
        }
    }

    internal Task PersistAsync(IRoomBot bot, CancellationToken ct)
    {
        _lastPersistedTileByBotId[bot.BotId] = _roomGrain.MapModule.ToIdx(bot.X, bot.Y);

        return _roomGrain
            ._grainFactory.GetRoomPersistenceGrain(_roomGrain.RoomId)
            .EnqueueDirtyBotAsync(bot.GetBotSnapshot(), ct);
    }

    internal Task PersistPositionIfMovedAsync(IRoomBot bot, CancellationToken ct)
    {
        if (bot.IsWalking)
            return Task.CompletedTask;

        var tileIdx = _roomGrain.MapModule.ToIdx(bot.X, bot.Y);

        if (_lastPersistedTileByBotId.TryGetValue(bot.BotId, out var last) && last == tileIdx)
            return Task.CompletedTask;

        return PersistAsync(bot, ct);
    }

    internal int NextRandom(int minInclusive, int maxExclusive) =>
        _random.Next(minInclusive, Math.Max(minInclusive + 1, maxExclusive));
}
