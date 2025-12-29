using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

public abstract class FurnitureWiredAddonLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx), IWiredAddon
{
    public override WiredType WiredType => WiredType.Addon;

    public abstract Task<bool> MutatePolicyAsync(IWiredContext ctx, CancellationToken ct);
    public abstract Task BeforeEffectsAsync(IWiredContext ctx, CancellationToken ct);
    public abstract Task AfterEffectsAsync(IWiredContext ctx, CancellationToken ct);
}
