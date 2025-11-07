using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Furniture.Abstractions;
using Turbo.Furniture.Configuration;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Furniture;

public sealed class FurnitureProvider(
    IOptions<FurnitureConfig> config,
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<IFurnitureProvider> logger
) : IFurnitureProvider
{
    private readonly FurnitureConfig _config = config.Value;
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<IFurnitureProvider> _logger = logger;
    private FurnitureSnapshot _current = Empty();

    public FurnitureSnapshot Current => _current;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .FurnitureDefinitions.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var defs = entities.Select(x => new FurnitureDefinitionSnapshot(
                x.Id,
                x.SpriteId,
                x.PublicName,
                x.ProductName,
                x.ProductType,
                x.Logic,
                x.TotalStates,
                x.X,
                x.Y,
                x.Z == 0 ? _config.MinimumZValue : x.Z,
                x.CanStack,
                x.CanWalk,
                x.CanSit,
                x.CanLay,
                x.CanRecycle,
                x.CanTrade,
                x.CanGroup,
                x.CanSell,
                x.UsagePolicy,
                x.ExtraData
            ));

            var defsById = defs.ToImmutableDictionary(p => p.Id);
            var snapshot = new FurnitureSnapshot(DefinitionsById: defsById);

            _logger.LogInformation(
                "Loaded furniture snapshot: TotalDefs={TotalDefCount}",
                snapshot.DefinitionsById.Count
            );

            Volatile.Write(ref _current, snapshot);
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static FurnitureSnapshot Empty() =>
        new(DefinitionsById: ImmutableDictionary<int, FurnitureDefinitionSnapshot>.Empty);
}
