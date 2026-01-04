using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

public abstract class FurnitureWiredAddonLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override WiredType WiredType => WiredType.Addon;

    public abstract Task<bool> MutatePolicyAsync(WiredProcessingContext ctx, CancellationToken ct);
    public abstract Task BeforeEffectsAsync(WiredProcessingContext ctx, CancellationToken ct);
    public abstract Task AfterEffectsAsync(WiredProcessingContext ctx, CancellationToken ct);
}
