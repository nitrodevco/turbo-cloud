using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

[RoomObjectLogic("wf_xtra_or_eval")]
public class WiredAddonConditionsEval(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.CONDITION_EVALUATION;

    public override Task<bool> MutatePolicyAsync(WiredProcessingContext ctx, CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    public override Task BeforeEffectsAsync(WiredProcessingContext ctx, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task AfterEffectsAsync(WiredProcessingContext ctx, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
