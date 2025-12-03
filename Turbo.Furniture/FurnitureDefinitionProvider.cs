using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Furniture.Configuration;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Furniture;

public sealed class FurnitureDefinitionProvider(
    IOptions<FurnitureConfig> config,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IFurnitureDefinitionProvider> logger
) : IFurnitureDefinitionProvider
{
    private readonly FurnitureConfig _config = config.Value;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<IFurnitureDefinitionProvider> _logger = logger;

    private ImmutableDictionary<int, FurnitureDefinitionSnapshot> _definitionsById =
        ImmutableDictionary<int, FurnitureDefinitionSnapshot>.Empty;

    public FurnitureDefinitionSnapshot? TryGetDefinition(int id) =>
        _definitionsById.TryGetValue(id, out var definition) ? definition : null;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

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
                x.ProductType,
                x.FurniCategory,
                x.Logic,
                x.TotalStates,
                x.Width,
                x.Length,
                x.StackHeight == 0 ? _config.MinimumZValue : x.StackHeight,
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

            _definitionsById = defs.ToImmutableDictionary(p => p.Id);

            _logger.LogInformation(
                "Loaded {TotalDefCount} furniture definitions",
                _definitionsById.Count
            );
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
