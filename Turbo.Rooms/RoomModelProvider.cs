using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Snapshots.Rooms;
using Turbo.Rooms.Abstractions;
using Turbo.Rooms.Mapping;

namespace Turbo.Rooms;

public sealed class RoomModelProvider(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<IRoomModelProvider> logger
) : IRoomModelProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<IRoomModelProvider> _logger = logger;
    private RoomModelsSnapshot _current = Empty();

    public RoomModelsSnapshot Current => _current;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .RoomModels.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var models = entities.Select(x => new RoomModelSnapshot(
                x.Id,
                x.Name,
                x.DoorX,
                x.DoorY,
                x.DoorRotation,
                RoomModelCompiler.CompileModelFromEntity(x)
            ));

            var modelsById = models.ToImmutableDictionary(p => p.Id);
            var snapshot = new RoomModelsSnapshot(modelsById);

            _logger.LogInformation(
                "Loaded room models snapshot: TotalModels={TotalModelCount}",
                snapshot.ModelsById.Count
            );

            Volatile.Write(ref _current, snapshot);
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static RoomModelsSnapshot Empty() =>
        new(ModelsById: ImmutableDictionary<int, RoomModelSnapshot>.Empty);
}
