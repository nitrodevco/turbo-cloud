using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;

/// <summary>A themed bar (energy, mana, cooldown...) of the variable on this tile; the style brings the icon and colour.</summary>
[RoomObjectLogic("wf_xtra_var_fx_status")]
public class WiredAddonVariableFxStatusBar(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableFxLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.VARIABLE_FX_STATUS_BAR;

    protected override VariableFxCategoryType Category => VariableFxCategoryType.StatusBar;
}
