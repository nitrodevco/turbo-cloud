using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;

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
    ];

    public async Task<bool> RequestPetCommandsAsync(
        ActionContext ctx,
        int petId,
        CancellationToken ct
    )
    {
        if (!TryGetPet(petId, out var pet))
            return false;

        await SendToPlayerAsync(
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
            _roomGrain._logger.LogDebug(
                "Pet {PetId} in room {RoomId} is too tired for {Command}",
                pet.PetId,
                _roomGrain.RoomId,
                command
            );

            return false;
        }

        TryGetPlayer(pet.OwnerId, out var owner);

        await _roomGrain.AvatarModule.StopWalkingAsync(pet, ct);

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
        pet.ActionExpiresAtMs = _roomGrain.NowMs() + durationMs;
    }

    internal async Task SpeakAsync(IRoomPet pet, CancellationToken ct)
    {
        if (pet.IsSilenced || Config.SpeechLines.Length == 0)
            return;

        var line = Config.SpeechLines[NextRandom(0, Config.SpeechLines.Length)];

        pet.AddStatus(AvatarStatusType.Gesture, AvatarStatusType.Speak.ToLegacyString());
        pet.ActionExpiresAtMs = _roomGrain.NowMs() + Config.SpeakDurationMs;

        await _roomGrain.SendComposerToRoomAsync(
            new ChatMessageComposer
            {
                ObjectId = pet.ObjectId,
                Text = line,
                Gesture = AvatarGestureType.None,
                StyleId = 0,
                Links = [],
                TrackingId = -1,
            },
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
        if (IsAdjacent(walker, target))
            return true;

        foreach (var (x, y) in TilesAround(target.X, target.Y))
        {
            if (!IsTileFreeForNpc(_roomGrain.MapModule.ToIdx(x, y)))
                continue;

            if (await _roomGrain.AvatarModule.WalkAvatarToAsync(walker, x, y, ct))
                return true;
        }

        return false;
    }

    internal (int X, int Y)[] TilesAround(int x, int y)
    {
        var map = _roomGrain.MapModule;
        var tiles = new System.Collections.Generic.List<(int, int)>();

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (map.InBounds(x + dx, y + dy))
                    tiles.Add((x + dx, y + dy));
            }
        }

        return [.. tiles.OrderBy(_ => NextRandom(0, int.MaxValue))];
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

        await PersistAsync(pet, ct);
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

        await SendToPlayerAsync(
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

        if (item is null)
            return false;

        pet.TargetItemId = item.ObjectId;

        if (pet.X == item.X && pet.Y == item.Y)
            return true;

        if (await _roomGrain.AvatarModule.WalkAvatarToAsync(pet, item.X, item.Y, ct))
            return true;

        pet.TargetItemId = -1;

        return false;
    }

    internal IRoomObject? FindSupplyItem(PetSupplyType type) =>
        _roomGrain
            ._state.ItemsById.Values.Where(x =>
                x.Logic is Object.Logic.Furniture.Floor.Pets.IPetSupplyLogic supply
                && supply.SupplyType == type
                && supply.HasSuppliesLeft
            )
            .OrderBy(_ => NextRandom(0, int.MaxValue))
            .FirstOrDefault();
}
