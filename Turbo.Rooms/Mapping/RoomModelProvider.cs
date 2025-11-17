using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;
using Turbo.Primitives.Rooms.Mapping;

namespace Turbo.Rooms.Mapping;

public sealed class RoomModelProvider(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<IRoomModelProvider> logger
) : IRoomModelProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<IRoomModelProvider> _logger = logger;
    private ImmutableDictionary<int, RoomModelSnapshot> _modelsById = ImmutableDictionary<
        int,
        RoomModelSnapshot
    >.Empty;

    public RoomModelSnapshot GetModelById(int modelId) =>
        _modelsById.TryGetValue(modelId, out var model)
            ? model
            : throw new KeyNotFoundException($"Room model not found: ModelId={modelId}");

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .RoomModels.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            _modelsById = entities
                .Select(x =>
                {
                    var modelData = RoomModelCompiler.CleanModelString(x.Model);
                    var compiledModel = RoomModelCompiler.CompileModelFromString(modelData);

                    return new RoomModelSnapshot
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Model = modelData,
                        DoorX = x.DoorX,
                        DoorY = x.DoorY,
                        DoorRotation = x.DoorRotation,
                        Width = compiledModel.Width,
                        Height = compiledModel.Height,
                        Size = compiledModel.Width * compiledModel.Height,
                        Heights = compiledModel.Heights,
                        States = compiledModel.States,
                    };
                })
                .ToImmutableDictionary(x => x.Id);

            _logger.LogInformation(
                "Loaded room models: TotalModels={TotalModelCount}",
                _modelsById.Count
            );
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
