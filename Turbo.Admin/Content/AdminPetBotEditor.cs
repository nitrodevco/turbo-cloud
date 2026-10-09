using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Pets;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Admin.Content;

/// <summary>
/// Pets and bots as staff manage them. Pet breeds and pet speech are rows the server keeps in its
/// providers: each change reloads them, so pets in rooms use it at once. A bot standing in a room
/// is its room's, and is changed through the room grain; a bot in an inventory can only be
/// deleted, through its owner's inventory grain.
/// </summary>
public sealed class AdminPetBotEditor(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IPetBreedProvider breeds,
    IPetSpeechProvider speech,
    IGrainFactory grainFactory,
    ILogger<AdminPetBotEditor> logger
)
{
    public const int SPEECH_MAX_LENGTH = 100;
    public const int BOT_PAGE_SIZE = 25;

    public async Task<PetContentResponse> GetPetsAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var breedRows = await dbCtx
            .PetBreeds.AsNoTracking()
            .OrderBy(x => x.TypeId)
            .ThenBy(x => x.PaletteId)
            .Select(x => new PetBreedItem(
                x.Id,
                x.TypeId,
                x.PaletteId,
                x.BreedId,
                x.RarityLevel,
                x.Sellable,
                x.Rare,
                x.ColorTag
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var lines = await dbCtx
            .PetSpeech.AsNoTracking()
            .OrderBy(x => x.TypeId)
            .ThenBy(x => x.Id)
            .Select(x => new PetSpeechItem(x.Id, x.TypeId, x.Line))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PetContentResponse(breedRows, lines);
    }

    /// <summary>Adds a palette of a pet type (row id 0) or changes one.</summary>
    public async Task<int> SaveBreedAsync(int id, PetBreedRequest request, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        PetBreedEntity row;

        if (id == 0)
        {
            if (request.TypeId is not >= 0 || request.PaletteId is not >= 0)
                throw new ArgumentException("A palette needs its pet type and its palette id.");

            if (
                await dbCtx
                    .PetBreeds.AnyAsync(
                        x => x.TypeId == request.TypeId && x.PaletteId == request.PaletteId,
                        ct
                    )
                    .ConfigureAwait(false)
            )
                throw new ArgumentException(
                    $"Pet type {request.TypeId} has a palette {request.PaletteId} already."
                );

            row = new PetBreedEntity
            {
                TypeId = request.TypeId.Value,
                PaletteId = request.PaletteId.Value,
                BreedId = request.BreedId ?? 0,
            };
            dbCtx.PetBreeds.Add(row);
        }
        else
        {
            row =
                await dbCtx.PetBreeds.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no pet palette {id}.");
        }

        if (request.RarityLevel is < 0)
            throw new ArgumentException("Rarity is 0 or more.");

        row.BreedId = request.BreedId ?? row.BreedId;
        row.RarityLevel = request.RarityLevel ?? row.RarityLevel;
        row.Sellable = request.Sellable ?? row.Sellable;
        row.Rare = request.Rare ?? row.Rare;
        row.ColorTag = request.ColorTag ?? row.ColorTag;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await breeds.ReloadAsync(ct).ConfigureAwait(false);

        return row.Id;
    }

    /// <summary>Adds a line a pet type says (row id 0; no type: every type without lines of its own), or changes one.</summary>
    public async Task<int> SaveSpeechAsync(int id, PetSpeechRequest request, CancellationToken ct)
    {
        var line = (request.Line ?? string.Empty).Trim();

        if (line.Length is 0 or > SPEECH_MAX_LENGTH)
            throw new ArgumentException($"A line is said, {SPEECH_MAX_LENGTH} characters at most.");

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        PetSpeechEntity row;

        if (id == 0)
        {
            row = new PetSpeechEntity { TypeId = request.TypeId, Line = line };
            dbCtx.PetSpeech.Add(row);
        }
        else
        {
            row =
                await dbCtx.PetSpeech.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no pet line {id}.");
            row.Line = line;
        }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await speech.ReloadAsync(ct).ConfigureAwait(false);

        return row.Id;
    }

    public async Task<bool> DeleteSpeechAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .PetSpeech.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        dbCtx.PetSpeech.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await speech.ReloadAsync(ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>Bots found by name, owner, bot or room id; newest first.</summary>
    public async Task<BotSearchResponse> SearchBotsAsync(
        string? query,
        int page,
        CancellationToken ct
    )
    {
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var bots = dbCtx.Bots.AsNoTracking();

        if (words.Length > 0)
            bots = int.TryParse(words, out var id)
                ? bots.Where(x => x.Id == id || x.RoomEntityId == id)
                : bots.Where(x => x.Name.Contains(words) || x.PlayerEntity!.Name == words);

        var total = await bots.CountAsync(ct).ConfigureAwait(false);
        var rows = await bots.OrderByDescending(x => x.Id)
            .Skip(Math.Max(0, page) * BOT_PAGE_SIZE)
            .Take(BOT_PAGE_SIZE)
            .Select(x => new BotItem(
                x.Id,
                x.Name,
                x.Motto,
                x.Figure,
                x.Gender,
                x.PlayerEntityId,
                x.PlayerEntity!.Name,
                x.RoomEntityId,
                x.RoomEntity == null ? null : x.RoomEntity.Name,
                x.ChatText ?? "",
                x.AutoChat,
                x.ChatDelaySeconds,
                x.MixSentences,
                x.FreeRoam,
                x.DanceType
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new BotSearchResponse(rows, total, BOT_PAGE_SIZE);
    }

    /// <summary>Sets a bot standing in a room, through its room; refused for one in an inventory.</summary>
    public async Task UpdateBotAsync(int id, BotStaffEditRequest request, CancellationToken ct)
    {
        var bot = await FindAsync(id, ct).ConfigureAwait(false);

        if (bot.RoomId is not { } roomId)
            throw new ArgumentException(
                "The bot is in its owner's inventory: it can be set once it stands in a room."
            );

        var updated = await grainFactory
            .GetRoomGrain(new RoomId(roomId))
            .StaffUpdateBotAsync(
                id,
                new BotStaffEditSnapshot
                {
                    Name = request.Name ?? string.Empty,
                    Motto = request.Motto ?? string.Empty,
                    Figure = request.Figure ?? string.Empty,
                    Gender = request.Gender ?? AvatarGenderType.Male,
                    ChatText = request.ChatText ?? string.Empty,
                    AutoChat = request.AutoChat ?? false,
                    ChatDelaySeconds = request.ChatDelaySeconds ?? 0,
                    MixSentences = request.MixSentences ?? false,
                    FreeRoam = request.FreeRoam ?? false,
                    Dance = request.Dance ?? AvatarDanceType.None,
                },
                ct
            )
            .ConfigureAwait(false);

        if (!updated)
            throw new ArgumentException(
                "Not saved: the name is too short or too long, or the bot has left the room."
            );
    }

    /// <summary>Takes a bot out of its room, back to its owner's inventory.</summary>
    public async Task PickupBotAsync(int id, CancellationToken ct)
    {
        var bot = await FindAsync(id, ct).ConfigureAwait(false);

        if (
            bot.RoomId is not { } roomId
            || !await grainFactory
                .GetRoomGrain(new RoomId(roomId))
                .StaffPickupBotAsync(id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException("The bot doesn't stand in a room.");

        logger.LogInformation("Bot {BotId} taken out of room {RoomId} by staff", id, roomId);
    }

    /// <summary>Deletes a bot from its owner's inventory; one standing in a room is taken out first.</summary>
    public async Task DeleteBotAsync(int id, CancellationToken ct)
    {
        var bot = await FindAsync(id, ct).ConfigureAwait(false);

        if (bot.RoomId is not null)
            throw new ArgumentException("Take the bot out of its room first.");

        if (
            !await grainFactory
                .GetInventoryGrain(new PlayerId(bot.OwnerId))
                .DeleteBotAsync(id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException("The bot is no longer in its owner's inventory.");

        logger.LogInformation("Bot {BotId} of player {OwnerId} deleted by staff", id, bot.OwnerId);
    }

    private async Task<(int OwnerId, int? RoomId)> FindAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var bot = await dbCtx
            .Bots.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.PlayerEntityId, x.RoomEntityId })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return bot is null
            ? throw new ArgumentException($"There is no bot {id}.")
            : (bot.PlayerEntityId, bot.RoomEntityId);
    }
}
