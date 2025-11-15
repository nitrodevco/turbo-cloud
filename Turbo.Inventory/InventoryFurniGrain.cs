using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Snapshots.Inventory;

namespace Turbo.Inventory;

public class InventoryFurniGrain(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<IInventoryFurniGrain> logger
) : Grain, IInventoryFurniGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<IInventoryFurniGrain> _logger = logger;
    private readonly Dictionary<int, InventoryFurniSnapshot> _snapshots = [];

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateFromExternalAsync(ct);

            _logger.LogInformation(
                "InventoryFurniGrain {PlayerId} activated.",
                this.GetPrimaryKeyLong()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error activating InventoryFurniGrain {PlayerId}",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "InventoryFurniGrain {PlayerId} deactivated.",
                this.GetPrimaryKeyLong()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deactivating InventoryFurniGrain {PlayerId}",
                this.GetPrimaryKeyLong()
            );

            throw;
        }

        return Task.CompletedTask;
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        using var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var entities = await dbCtx
                .Furnitures.AsNoTracking()
                .Where(x => x.RoomEntityId != null && x.PlayerEntityId == this.GetPrimaryKeyLong())
                .ToListAsync(ct);

            foreach (var entity in entities)
            {
                if (entity is null)
                    continue;

                var snapshot = new InventoryFurniSnapshot(
                    entity.Id,
                    entity.PlayerEntityId,
                    entity.FurnitureDefinitionEntityId,
                    entity.StuffData
                );

                AddSnapshot(snapshot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error hydrating PlayerGrain {PlayerId} from database.",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    private void AddSnapshot(in InventoryFurniSnapshot snapshot)
    {
        _snapshots[snapshot.Id] = snapshot;
    }
}
