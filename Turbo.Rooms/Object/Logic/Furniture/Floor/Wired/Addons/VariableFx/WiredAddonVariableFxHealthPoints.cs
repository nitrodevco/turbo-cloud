using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;

/// <summary>Hearts or a health bar over whoever holds the variable on this tile.</summary>
[RoomObjectLogic("wf_xtra_var_fx_health")]
public class WiredAddonVariableFxHealthPoints(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableFxLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.VARIABLE_FX_HEALTH_POINTS;

    protected override VariableFxCategoryType Category => VariableFxCategoryType.HealthPoints;
}
