using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Furniture.Abstractions;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Furniture;

public sealed class FurnitureProvider(IDbContextFactory<TurboDbContext> dbContextFactory)
    : IFurnitureProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
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
                x.ProductName
            ));

            var defsById = defs.ToImmutableDictionary(p => p.Id);

            var snapshot = new FurnitureSnapshot(DefinitionsById: defsById);

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
