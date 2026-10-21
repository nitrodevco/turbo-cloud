using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomPetModule
{
    /// <summary>Statuses an action leaves on the pet until it expires or the next command.</summary>
    private static readonly AvatarStatusType[] ACTION_STATUSES =
    [
        AvatarStatusType.Beg,
        AvatarStatusType.Dead,
        AvatarStatusType.Jump,
        AvatarStatusType.Play,
        AvatarStatusType.Kick,
        AvatarStatusType.Eat,
        AvatarStatusType.Gesture,
        AvatarStatusType.WagTail,
        AvatarStatusType.EyeBlink,
    ];

    public async Task<bool> RequestPetCommandsAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    )
    {
        if (!TryGetPet(petId, out var pet))
            return false;

        await GrainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new PetCommandsMessageComposer
            {
                PetId = petId,
                AllCommands = AllCommandsFor(pet),
                EnabledCommands = EnabledCommandsFor(pet),
            },
            ct
        );

        return true;
    }

    private ImmutableArray<PetCommandType> AllCommandsFor(IRoomPet pet) =>
        [
            .. Config
                .CommandUnlockLevels.Keys.Where(x =>
                    x != PetCommandType.Breed || PetTypes.CanNestBreed(pet.TypeId)
                )
                .OrderBy(x => (int)x),
        ];

    private ImmutableArray<PetCommandType> EnabledCommandsFor(IRoomPet pet) =>
        [.. AllCommandsFor(pet).Where(x => IsCommandEnabled(pet, x))];

    internal bool IsCommandEnabled(IRoomPet pet, PetCommandType command) =>
        Config.CommandUnlockLevels.TryGetValue(command, out var level) && pet.Level >= level;

    /// <summary>
    /// Chat of the form "&lt;pet name&gt; &lt;command word&gt;" addressed to one of the speaker's
    /// pets in the room. The message still reaches the room as normal chat.
    /// </summary>
    internal async Task HandleChatAsync(PlayerChatEvent evt, CancellationToken ct)
    {
        if (evt.ChatType == RoomChatType.Whisper)
            return;

        foreach (var pet in Pets.Where(x => x.OwnerId == evt.PlayerId).ToList())
        {
            if (!TryParseCommand(evt.Text, pet, out var command))
                continue;

            if (!IsCommandEnabled(pet, command))
                continue;

            await ExecuteCommandAsync(evt.CausedBy, pet, command, ct);

            return;
        }
    }

    private bool TryParseCommand(string text, IRoomPet pet, out PetCommandType command)
    {
        command = PetCommandType.Free;

        var name = pet.Name;

        if (
            text.Length <= name.Length + 1
            || !text.StartsWith(name, StringComparison.OrdinalIgnoreCase)
            || text[name.Length] != ' '
        )
            return false;

        var word = text[(name.Length + 1)..].Trim();

        foreach (var (candidate, words) in Config.CommandWords)
        {
            if (words.Any(x => string.Equals(x, word, StringComparison.OrdinalIgnoreCase)))
            {
                command = candidate;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Carries out a command. A tired or unfed pet ignores it; an obeyed one costs a little
    /// energy and earns experience.
    /// </summary>
    internal async Task<bool> ExecuteCommandAsync(
        ActionContext ctx,
        IRoomPet pet,
        PetCommandType command,
        CancellationToken ct
    )
    {
        if (pet.IsMonsterplant || pet.IsRiding)
            return false;

        if (pet.Energy < Config.CommandEnergyCost || pet.Nutrition < Config.CommandNutritionCost)
        {
            Logger.LogDebug(
                "Pet {PetId} in room {RoomId} is too tired for {Command}",
                pet.PetId,
                _roomGrain.RoomId,
                command
            );

            return false;
        }

        if (
            command is PetCommandType.Sit or PetCommandType.LieDown or PetCommandType.Nest
            && !CanRest(pet)
        )
            return false;

        AvatarModule.TryGetPlayer(pet.OwnerId, out var owner);

        await AvatarModule.StopWalkingAsync(pet, ct);

        ClearActionStatuses(pet);

        pet.FollowObjectId = -1;
        pet.TargetItemId = -1;
        pet.IsFreeRoaming = false;

        switch (command)
        {
            case PetCommandType.Free:
                pet.Sit(false);
                pet.Lay(false);
                pet.IsFreeRoaming = true;
                break;
            case PetCommandType.Sit:
                pet.Sit(true);
                break;
            case PetCommandType.LieDown:
                pet.Lay(true);
                break;
            case PetCommandType.ComeHere:
                if (owner is not null)
                    await WalkNextToAsync(pet, owner, ct);
                break;
            case PetCommandType.Beg:
                SetTimedStatus(pet, AvatarStatusType.Beg, Config.ActionDurationMs);
                break;
            case PetCommandType.PlayDead:
                SetTimedStatus(pet, AvatarStatusType.Dead, Config.ActionDurationMs);
                break;
            case PetCommandType.Stay:
                pet.Sit(false);
                pet.Lay(false);
                break;
            case PetCommandType.Follow:
            case PetCommandType.FollowLeft:
            case PetCommandType.FollowRight:
                pet.Sit(false);
                pet.Lay(false);
                pet.FollowObjectId = owner?.ObjectId ?? -1;
                pet.FollowOffset = command switch
                {
                    PetCommandType.FollowLeft => -1,
                    PetCommandType.FollowRight => 1,
                    _ => 0,
                };
                break;
            case PetCommandType.Stand:
                pet.Sit(false);
                pet.Lay(false);
                break;
            case PetCommandType.Jump:
                SetTimedStatus(pet, AvatarStatusType.Jump, Config.ActionDurationMs);
                break;
            case PetCommandType.Speak:
                await SpeakAsync(pet, ct);
                break;
            case PetCommandType.Play:
                SetTimedStatus(pet, AvatarStatusType.Play, Config.ActionDurationMs);
                break;
            case PetCommandType.Silent:
                pet.IsSilenced = true;
                break;
            case PetCommandType.Nest:
                if (!await WalkToSupplyAsync(pet, PetSupplyType.Nest, ct))
                    return false;
                break;
            case PetCommandType.Drink:
                if (!await WalkToSupplyAsync(pet, PetSupplyType.Drink, ct))
                    return false;
                break;
            case PetCommandType.PlayFootball:
                SetTimedStatus(pet, AvatarStatusType.Kick, Config.ActionDurationMs);
                break;
            case PetCommandType.Breed:
                if (!await GoToBreedingNestAsync(ctx, pet, ct))
                    return false;
                break;
            default:
                return false;
        }

        pet.SetEnergy(pet.Energy - Config.CommandEnergyCost);
        pet.SetNutrition(pet.Nutrition - Config.CommandNutritionCost);
        pet.MarkDirty();

        await AddExperienceAsync(pet, Config.CommandExperience, ct);

        return true;
    }

    internal void ClearActionStatuses(IRoomPet pet)
    {
        pet.RemoveStatus(ACTION_STATUSES);
        pet.ActionExpiresAtMs = 0;
    }

    private void SetTimedStatus(IRoomPet pet, AvatarStatusType status, int durationMs)
    {
        pet.Sit(false);
        pet.Lay(false);
        pet.AddStatus(status, string.Empty);
        pet.ActionExpiresAtMs = NowMs() + durationMs;
    }

    internal async Task SpeakAsync(IRoomPet pet, CancellationToken ct)
    {
        // Each type says its own things (a cat does not bark); pet_speech holds them.
        var lines = _roomGrain._petSpeechProvider.GetLines(pet.TypeId);

        if (pet.IsSilenced || lines.IsDefaultOrEmpty)
            return;

        var line = lines[NextRandom(0, lines.Length)];

        pet.AddStatus(AvatarStatusType.Gesture, AvatarStatusType.Speak.ToLegacyString());
        pet.ActionExpiresAtMs = NowMs() + Config.SpeakDurationMs;

        await ChatSystem.SayAsAvatarAsync(pet, line, new AvatarSpeech(), ct);
    }

    /// <summary>
    /// Says one of the hotel's lines for what the pet is doing: <c>pet.vocals.&lt;type&gt;.&lt;vocal&gt;.&lt;n&gt;</c>
    /// (Habbo's texts carry them, pet.vocals.15.DRINKING.0 "Ah, that's the good stuff."; the client
    /// never reads them, so the room says them). Nothing when the hotel has none for the type.
    /// </summary>
    internal async Task SayVocalAsync(IRoomPet pet, string vocal, CancellationToken ct)
    {
        if (pet.IsSilenced || !Chance(Config.VocalChancePercent))
            return;

        var prefix = $"pet.vocals.{pet.TypeId}.{vocal}.";
        var texts = await _roomGrain._hotelTextProvider.GetTextsByPrefixAsync(prefix, ct);
        var lines = new List<string>(texts.Count);

        for (var i = 0; i < texts.Count; i++)
        {
            if (texts.TryGetText(prefix + i, out var line))
                lines.Add(line);
        }

        if (lines.Count == 0)
            return;

        await ChatSystem.SayAsAvatarAsync(
            pet,
            lines[NextRandom(0, lines.Count)],
            new AvatarSpeech(),
            ct
        );
    }

    /// <summary>Walks <paramref name="walker"/> to a free tile next to <paramref name="target"/>; false when none is reachable.</summary>
    internal async Task<bool> WalkNextToAsync(
        IRoomAvatar walker,
        IRoomAvatar target,
        CancellationToken ct
    )
    {
        if (RoomAvatarModule.AreAdjacent(walker, target))
            return true;

        foreach (var (x, y) in TilesAround(target.X, target.Y))
        {
            if (!IsTileFreeForNpc(MapModule.ToIdx(x, y)))
                continue;

            if (await AvatarModule.WalkAvatarToAsync(walker, x, y, ct))
                return true;
        }

        return false;
    }

    /// <summary>The tiles around one, in a random order, so a pet does not always pick the same side.</summary>
    internal (int X, int Y)[] TilesAround(int x, int y)
    {
        var map = MapModule;
        var tiles = new (int X, int Y)[8];
        var count = 0;

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (map.InBounds(x + dx, y + dy))
                    tiles[count++] = (x + dx, y + dy);
            }
        }

        var around = count == tiles.Length ? tiles : tiles[..count];

        _random.Shuffle(around);

        return around;
    }

    internal async Task AddExperienceAsync(IRoomPet pet, int amount, CancellationToken ct)
    {
        if (amount <= 0 || pet.IsMonsterplant)
            return;

        pet.SetExperience(pet.Experience + amount);

        await _roomGrain.SendComposerToRoomAsync(
            new PetExperienceMessageComposer
            {
                PetId = pet.PetId,
                ObjectId = pet.ObjectId,
                GainedExperience = amount,
            },
            ct
        );

        while (pet.Level < MaxLevelFor(pet) && pet.Experience >= ExperienceToLevel(pet.Level))
            await LevelUpAsync(pet, ct);

        Persist(pet);
    }

    internal async Task LevelUpAsync(IRoomPet pet, CancellationToken ct)
    {
        pet.SetLevel(pet.Level + 1);

        RefreshFlags(pet);
        RefreshPosture(pet);

        await _roomGrain.SendComposerToRoomAsync(
            new PetLevelUpdateMessageComposer
            {
                ObjectId = pet.ObjectId,
                PetId = pet.PetId,
                Level = pet.Level,
            },
            ct
        );

        await GrainFactory.SendComposerToPlayerAsync(
            pet.OwnerId,
            new PetLevelNotificationEventMessageComposer
            {
                PetId = pet.PetId,
                Name = pet.Name,
                Level = pet.Level,
                Figure = pet.PetFigure,
            },
            ct
        );

        await BroadcastStatusAsync(pet, ct);
    }

    /// <summary>Walks the pet to a supply item of the wanted kind; false when the room has none free.</summary>
    internal async Task<bool> WalkToSupplyAsync(
        IRoomPet pet,
        PetSupplyType type,
        CancellationToken ct
    )
    {
        var item = FindSupplyItem(type);

        return item is not null && await WalkToSupplyItemAsync(pet, item, ct);
    }

    /// <summary>Walks the pet to the supply item; false when there is no way to it.</summary>
    internal async Task<bool> WalkToSupplyItemAsync(
        IRoomPet pet,
        IRoomObject item,
        CancellationToken ct
    )
    {
        pet.TargetItemId = item.ObjectId;

        if (IsAtItem(pet, item))
            return true;

        if (CanStandOn(item))
        {
            if (await AvatarModule.WalkAvatarToAsync(pet, item.X, item.Y, ct))
                return true;
        }
        else
        {
            foreach (var (x, y) in SidesOf(item))
            {
                if (!IsTileFreeForNpc(MapModule.ToIdx(x, y)))
                    continue;

                if (await AvatarModule.WalkAvatarToAsync(pet, x, y, ct))
                    return true;
            }
        }

        pet.TargetItemId = -1;

        return false;
    }

    /// <summary>
    /// Whether the pet is where it uses the item: on it, or for an item it cannot stand on (a
    /// horse trough) on a tile beside it. Official horses drank at a walkable water bowl standing
    /// on it, and at a trough from the tile next to it, facing it (official client capture
    /// 2026-10-17, room "saddle club": 4,1 facing 4,2 and 5,3 facing 4,3).
    /// </summary>
    internal bool IsAtItem(IRoomPet pet, IRoomObject item)
    {
        if (pet.X == item.X && pet.Y == item.Y)
            return true;

        if (CanStandOn(item))
            return false;

        foreach (var (x, y) in SidesOf(item))
        {
            if (pet.X == x && pet.Y == y)
                return true;
        }

        return false;
    }

    /// <summary>Whether a pet may stand on the item's tile, whoever stands there now.</summary>
    private bool CanStandOn(IRoomObject item)
    {
        var idx = MapModule.ToIdx(item.X, item.Y);

        if (!MapModule.InBounds(idx))
            return false;

        var flags = State.TileFlags[idx];

        return !flags.Has(RoomTileFlags.Disabled)
            && !flags.Has(RoomTileFlags.Closed)
            && (!flags.Has(RoomTileFlags.FurnitureOccupied) || flags.Has(RoomTileFlags.Walkable));
    }

    /// <summary>
    /// The tiles beside an item's footprint, sharing an edge with it, in a random order. Corners
    /// are left out: the official horses drank from a side every time (5 of 5 trough drinks) with
    /// free corner tiles next to the trough.
    /// </summary>
    private (int X, int Y)[] SidesOf(IRoomObject item)
    {
        var map = MapModule;
        var footprint = new HashSet<int>();

        if (
            item is IRoomFloorItem floorItem
            && map.GetTileIdForSize(
                item.X,
                item.Y,
                item.Rotation,
                floorItem.Definition.Width,
                floorItem.Definition.Length,
                out var tileIds
            )
        )
            footprint.UnionWith(tileIds);
        else
            footprint.Add(map.ToIdx(item.X, item.Y));

        var sides = new List<(int X, int Y)>();

        foreach (var idx in footprint)
        {
            var (x, y) = map.GetTileXY(idx);

            foreach (var (sx, sy) in new[] { (x, y - 1), (x + 1, y), (x, y + 1), (x - 1, y) })
            {
                if (
                    map.InBounds(sx, sy)
                    && !footprint.Contains(map.ToIdx(sx, sy))
                    && !sides.Contains((sx, sy))
                )
                    sides.Add((sx, sy));
            }
        }

        var shuffled = sides.ToArray();

        _random.Shuffle(shuffled);

        return shuffled;
    }

    /// <summary>
    /// A supply item of the wanted kind with something left, picked at random among them. One
    /// pass with a reservoir of one: every candidate is equally likely, as when the whole list
    /// was shuffled to take its first.
    /// </summary>
    internal IRoomObject? FindSupplyItem(PetSupplyType type)
    {
        IRoomObject? picked = null;
        var seen = 0;

        foreach (var item in FurniModule.Items)
        {
            if (
                item.Logic
                    is not Object.Logic.Furniture.Floor.Pets.IPetSupplyLogic
                    {
                        HasSuppliesLeft: true,
                    } supply
                || supply.SupplyType != type
            )
                continue;

            seen++;

            if (_random.Next(seen) == 0)
                picked = item;
        }

        return picked;
    }
}
