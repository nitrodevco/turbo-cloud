using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>Runs one action per firing, cycling through the stack so each runs before any repeats.</summary>
[RoomObjectLogic("wf_xtra_unseen")]
public class WiredAddonUnseenActions(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.INEDITED_ACTION;

    // Never flashes, also when it picked an effect (official client capture 2026-10-24T10-52-39,
    // 2026-10-25T01-51-02, wired matrix addon.2.R3 2/2; golden replay fixture 0/2).
    public override bool FlashesWithStack => false;

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.EffectMode = WiredEffectModeType.Unseen;

        return Task.FromResult(true);
    }
}
