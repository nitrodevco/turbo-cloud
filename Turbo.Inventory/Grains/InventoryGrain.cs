using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Grains;

namespace Turbo.Inventory.Grains;

public sealed partial class InventoryGrain : Grain, IInventoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory;
    private readonly IFurnitureItemsLoader _furnitureItemsLoader;

    private readonly InventoryLiveState _state;

    public InventoryGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IFurnitureItemsLoader furnitureItemsLoader
    )
    {
        _dbContextFactory = dbContextFactory;
        _furnitureItemsLoader = furnitureItemsLoader;

        _state = new();
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await LoadFurnitureAsync(ct);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private async Task LoadFurnitureAsync(CancellationToken ct)
    {
        var (floorItems, wallItems) = await _furnitureItemsLoader
            .LoadByPlayerIdAsync(this.GetPrimaryKeyLong(), ct)
            .ConfigureAwait(false);

        foreach (var item in floorItems)
            await AddFloorItemAsync(item, ct);

        foreach (var item in wallItems)
        {
            //
        }
    }
}
