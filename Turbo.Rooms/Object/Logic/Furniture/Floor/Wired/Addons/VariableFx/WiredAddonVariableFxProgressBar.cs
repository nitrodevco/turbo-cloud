using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;

/// <summary>A plain progress bar of the variable on this tile between its minimum and maximum.</summary>
[RoomObjectLogic("wf_xtra_var_fx_progress")]
public class WiredAddonVariableFxProgressBar(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableFxLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.VARIABLE_FX_PROGRESS_BAR;

    protected override VariableFxCategoryType Category => VariableFxCategoryType.ProgressBar;
}
