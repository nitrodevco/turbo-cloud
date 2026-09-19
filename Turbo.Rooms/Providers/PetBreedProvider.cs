using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Rooms.Providers;

/// <summary>The <c>pet_breeds</c> table, loaded at startup and on demand.</summary>
public sealed class PetBreedProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IPetBreedProvider> logger
) : IPetBreedProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<IPetBreedProvider> _logger = logger;

    private ImmutableDictionary<int, ImmutableArray<PetBreedSnapshot>> _palettesByTypeId =
        ImmutableDictionary<int, ImmutableArray<PetBreedSnapshot>>.Empty;

    public ImmutableArray<PetBreedSnapshot> GetPalettes(int typeId) =>
        _palettesByTypeId.TryGetValue(typeId, out var palettes) ? palettes : [];

    public PetBreedSnapshot? TryGetPalette(int typeId, int paletteId) =>
        GetPalettes(typeId).FirstOrDefault(x => x.PaletteId == paletteId);

    public async Task ReloadAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx.PetBreeds.AsNoTracking().ToListAsync(ct);

        _palettesByTypeId = entities
            .GroupBy(x => x.TypeId)
            .ToImmutableDictionary(
                group => group.Key,
                group =>
                    group.OrderBy(x => x.PaletteId).Select(x => x.ToSnapshot()).ToImmutableArray()
            );

        if (entities.Count == 0)
            _logger.LogWarning(
                "No pet breeds are defined; the catalog pet pages and breeding have nothing to offer"
            );
        else
            _logger.LogInformation(
                "Loaded {Count} pet palettes for {Types} pet types",
                entities.Count,
                _palettesByTypeId.Count
            );
    }
}
