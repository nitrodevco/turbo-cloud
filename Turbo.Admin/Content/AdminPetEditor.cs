using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Pets;
using Turbo.Primitives.Pets.Providers;

namespace Turbo.Admin.Content;

/// <summary>
/// Pets as staff manage them. Pet breeds and pet speech are rows the server keeps in its
/// providers: each change reloads them, so pets in rooms use it at once.
/// </summary>
public sealed class AdminPetEditor(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IPetBreedProvider breeds,
    IPetSpeechProvider speech
)
{
    public const int SPEECH_MAX_LENGTH = 100;

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
}
