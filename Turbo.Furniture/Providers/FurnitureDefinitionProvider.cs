using System;
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
using Turbo.Primitives.Furniture.Providers;
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

            var defs = entities
                .Select(x => new FurnitureDefinitionSnapshot
                {
                    Id = x.Id,
                    SpriteId = x.SpriteId,
                    Name = x.Name,
                    ProductType = x.ProductType,
                    FurniCategory = x.FurniCategory,
                    LogicName = x.Logic,
                    TotalStates = x.TotalStates,
                    Width = x.Width,
                    Length = x.Length,
                    StackHeight = Math.Round(Math.Max(_config.MinimumZValue, x.StackHeight), 2),
                    CanStack = x.CanStack,
                    CanWalk = x.CanWalk,
                    CanSit = x.CanSit,
                    CanLay = x.CanLay,
                    CanRecycle = x.CanRecycle,
                    CanTrade = x.CanTrade,
                    CanGroup = x.CanGroup,
                    CanSell = x.CanSell,
                    UsagePolicy = x.UsagePolicy,
                    ExtraData = x.ExtraData,
                })
                .ToList();

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
