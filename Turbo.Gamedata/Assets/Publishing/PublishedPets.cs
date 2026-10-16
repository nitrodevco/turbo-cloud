using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Assets.Nitro;
using Turbo.Database.Context;
using Turbo.Database.Entities.Pets;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// The pets a publish sends, made known to the client and the server before it does:
/// <list type="bullet">
/// <item><c>renderer.petTypes</c>, the client's pet types by type id (a type's id is its place
/// in the list, as Habbo's <c>pet.configuration</c> numbers them): its order is kept, a Habbo
/// pet bundle fills the places its type ids name, and the hotel's own pet bundles come after.
/// A place the hotel set otherwise stays as it set it.</item>
/// <item><c>pet_breeds</c>: each palette a type's bundle can be drawn in that has no row yet -
/// not a master palette, nor one for a part alone (hair, a tail, a hat) - with its breed and
/// colour tag as the bundle has them. A row there is kept as the hotel set it.</item>
/// </list>
/// </summary>
internal sealed class PublishedPets(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAssetBundleStore store,
    IGamedataVariableService variables,
    IPetBreedProvider breeds,
    ILogger<PublishedPets> logger
)
{
    public const string PET_TYPES_VARIABLE = "renderer.petTypes";

    private const string BODY_TAG = "body";

    /// <summary>What it did, in words for the job's log.</summary>
    public async Task<string> ApplyAsync(PlayerId player, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var bundles = await dbCtx
            .AssetBundles.AsNoTracking()
            .Where(x => x.Kind == AssetBundleKind.Pet && x.Hash != null)
            .Select(x => new { x.Name, x.Ids })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var stored = await dbCtx
            .GamedataVariables.AsNoTracking()
            .Where(x => x.Key == PET_TYPES_VARIABLE)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        var before = TypesOf(stored);
        var types = Arrange(
            before,
            [
                .. bundles
                    .Where(x => store.IsValidName(x.Name))
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .Select(x => (x.Name, AssetBundleIds.Parse(x.Ids))),
            ],
            out var clashes
        );

        foreach (var (type, hotel, bundle) in clashes)
            logger.LogWarning(
                "Pet type {Type} is {Hotel} in {Variable}; the {Bundle} bundle Habbo numbers it by is left out",
                type,
                hotel,
                PET_TYPES_VARIABLE,
                bundle
            );

        var changedTypes = !types.SequenceEqual(before, StringComparer.Ordinal);

        if (changedTypes)
            await variables
                .SaveAsync(PET_TYPES_VARIABLE, JsonSerializer.Serialize(types), player, ct)
                .ConfigureAwait(false);

        var added = await AddBreedsAsync(dbCtx, types, ct).ConfigureAwait(false);

        if (added > 0)
            await breeds.ReloadAsync(ct).ConfigureAwait(false);

        if (changedTypes || added > 0)
            logger.LogInformation(
                "Publishing pets: {Types} pet types ({Added} new), {Breeds} breeds added",
                types.Count,
                types.Count - before.Count,
                added
            );

        return $"Pets: {types.Count} types{(changedTypes ? $", {PET_TYPES_VARIABLE} changed" : "")}, {added} breeds added"
            + (clashes.Count > 0 ? $", {clashes.Count} types kept as the hotel set them." : ".");
    }

    /// <summary>
    /// The pet types from the hotel's list and the bundles: the list as it is, each bundle placed at
    /// the type ids it has (the list grown to them), and a bundle with none, or of which no id
    /// could be placed, after the rest when the list lacks it.
    /// </summary>
    internal static List<string> Arrange(
        IReadOnlyList<string> current,
        IReadOnlyList<(string Name, System.Collections.Immutable.ImmutableArray<int> Ids)> bundles,
        out List<(int Type, string Hotel, string Bundle)> clashes
    )
    {
        var types = current.ToList();
        var unplaced = new List<string>();

        clashes = [];

        foreach (var (name, ids) in bundles)
        {
            var placed = false;

            foreach (var id in ids.Where(x => x >= 0).Order())
            {
                while (types.Count <= id)
                    types.Add(string.Empty);

                if (types[id].Length == 0 || types[id] == name)
                {
                    types[id] = name;
                    placed = true;
                }
                else
                {
                    clashes.Add((id, types[id], name));
                }
            }

            if (!placed && !types.Contains(name, StringComparer.Ordinal))
                unplaced.Add(name);
        }

        types.AddRange(unplaced);

        return types;
    }

    /// <summary>The breed rows a type's bundle palettes give: <c>(palette, breed, colour tag)</c>.</summary>
    internal static List<(int Palette, int Breed, int ColorTag)> BreedsOf(JsonNode? asset)
    {
        var found = new List<(int, int, int)>();

        if (asset?["palettes"] is not JsonArray palettes)
            return found;

        foreach (var palette in palettes.OfType<JsonObject>())
        {
            if (
                palette["id"]?.GetValue<int>() is not { } id
                || palette["master"]?.GetValue<bool>() == true
            )
                continue;

            if (
                palette["tags"] is JsonArray tags
                && tags.Count > 0
                && !tags.Any(x => x?.GetValue<string>() == BODY_TAG)
            )
                continue;

            found.Add(
                (
                    id,
                    palette["breed"]?.GetValue<int>() ?? id,
                    palette["colorTag"]?.GetValue<int>() ?? -1
                )
            );
        }

        return found;
    }

    private async Task<int> AddBreedsAsync(
        TurboDbContext dbCtx,
        IReadOnlyList<string> types,
        CancellationToken ct
    )
    {
        var have = (
            await dbCtx
                .PetBreeds.AsNoTracking()
                .Select(x => new { x.TypeId, x.PaletteId })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .Select(x => (x.TypeId, x.PaletteId))
            .ToHashSet();
        var palettes = new Dictionary<string, List<(int Palette, int Breed, int ColorTag)>>(
            StringComparer.Ordinal
        );
        var added = 0;

        for (var type = 0; type < types.Count; type++)
        {
            var name = types[type];

            if (name.Length == 0 || !store.IsValidName(name))
                continue;

            if (!palettes.TryGetValue(name, out var found))
                palettes[name] = found = await ReadBreedsAsync(name, ct).ConfigureAwait(false);

            foreach (var (palette, breed, colorTag) in found)
            {
                if (!have.Add((type, palette)))
                    continue;

                dbCtx.PetBreeds.Add(
                    new PetBreedEntity
                    {
                        TypeId = type,
                        PaletteId = palette,
                        BreedId = breed,
                        ColorTag = colorTag,
                    }
                );
                added++;
            }
        }

        if (added > 0)
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        return added;
    }

    /// <summary>A pet bundle's breeds; none when it has no file, or the file does not read.</summary>
    private async Task<List<(int, int, int)>> ReadBreedsAsync(string name, CancellationToken ct)
    {
        var path = store.FullPathOf(AssetBundleKind.Pet, name);

        if (!File.Exists(path))
            return [];

        try
        {
            var bundle = NitroBundle.Read(
                await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false)
            );

            return bundle.Files.TryGetValue($"{name}.json", out var json)
                ? BreedsOf(JsonNode.Parse(json))
                : [];
        }
        catch (Exception ex)
            when (ex
                    is InvalidDataException
                        or JsonException
                        or InvalidOperationException
                        or FormatException
            )
        {
            logger.LogWarning(ex, "The pet bundle {Name} gives no breeds: it does not read", name);

            return [];
        }
    }

    private static List<string> TypesOf(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return [];

        try
        {
            return JsonNode.Parse(stored) is JsonArray array
                ?
                [
                    .. array.Select(x =>
                        x is JsonValue value && value.TryGetValue<string>(out var name)
                            ? name
                            : string.Empty
                    ),
                ]
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
