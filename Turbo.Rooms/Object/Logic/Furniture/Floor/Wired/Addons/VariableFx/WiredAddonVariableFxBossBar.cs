using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;

/// <summary>The wide boss health bar of the variable on this tile.</summary>
[RoomObjectLogic("wf_xtra_var_fx_boss")]
public class WiredAddonVariableFxBossBar(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableFxLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.VARIABLE_FX_BOSS_BAR;

    protected override VariableFxCategoryType Category => VariableFxCategoryType.BossBar;
}
