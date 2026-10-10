using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets;

/// <summary>
/// What the hotel names, and so which bundles it uses: the asset names of its furniture
/// definitions (a definition's name without its <c>*N</c> colour) and the pet types its breeds
/// are of. An effect or clothing bundle is always used: the client may be asked for any effect,
/// and figuredata names clothing by part, not by library.
/// </summary>
internal sealed class AssetBundleUsage
{
    private AssetBundleUsage(HashSet<string> furniture, HashSet<int> petTypes)
    {
        Furniture = furniture;
        PetTypes = petTypes;
    }

    /// <summary>The asset names the furniture definitions use.</summary>
    public HashSet<string> Furniture { get; }

    /// <summary>The pet types with breeds.</summary>
    public HashSet<int> PetTypes { get; }

    public static async Task<AssetBundleUsage> LoadAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    )
    {
        var names = await dbCtx
            .FurnitureDefinitions.AsNoTracking()
            .Select(x => x.Name)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var petTypes = await dbCtx
            .PetBreeds.AsNoTracking()
            .Select(x => x.TypeId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new AssetBundleUsage(
            names.Select(HabboFurnitureFiles.AssetName).ToHashSet(StringComparer.Ordinal),
            [.. petTypes]
        );
    }

    /// <summary>Whether the hotel names the bundle of this kind, name and ids (as its row keeps them).</summary>
    public bool IsUsed(AssetBundleKind kind, string name, string? ids) =>
        kind switch
        {
            AssetBundleKind.Furniture => Furniture.Contains(name),
            AssetBundleKind.Pet => AssetBundleIds.Parse(ids).Any(PetTypes.Contains),
            _ => true,
        };
}
