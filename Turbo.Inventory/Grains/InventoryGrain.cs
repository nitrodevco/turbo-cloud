using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Grains;

namespace Turbo.Inventory.Grains;

public sealed partial class InventoryGrain : Grain, IInventoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly IFurnitureItemsLoader _furnitureItemsLoader;

    private readonly InventoryLiveState _state;
    private readonly InventoryFurniModule _furniModule;

    public InventoryGrain(
        IDbContextFactory<TurboDbContext> dbContextFactory,
        IGrainFactory grainFactory,
        IFurnitureItemsLoader furnitureItemsLoader
    )
    {
        _dbCtxFactory = dbContextFactory;
        _grainFactory = grainFactory;
        _furnitureItemsLoader = furnitureItemsLoader;

        _state = new();
        _furniModule = new InventoryFurniModule(this, _state, _furnitureItemsLoader);
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task SendComposerAsync(IComposer composer, CancellationToken ct)
    {
        var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(this.GetPrimaryKeyLong());

        return playerPresence.SendComposerAsync(composer, ct);
    }
}
