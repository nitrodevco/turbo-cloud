using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// "Variable Add-on: Share" (<c>wf_xtra_var_share</c>, AddonCodes.VARIABLE_ADDON_SHARE 1003,
/// AS3 <c>VariableAddonShareAddon</c> WIN63-202610201032): placed on a permanently shared
/// variable, it shares the variable add-ons of the ticked categories on that stack with the
/// rooms using the variable through "From Another Room" (Wired Faculty 19/10/2026;
/// <c>addon_share.usage_info</c>). Its "Synchronize" button sends
/// <c>SynchronizeSharedAddons</c>.
/// </summary>
[RoomObjectLogic("wf_xtra_var_share")]
public class WiredAddonVariableShare(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : WiredAddonVariableSharing(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredAddonType.VARIABLE_ADDON_SHARE;

    /// <summary>The shared variable this add-on stands on, or null.</summary>
    public FurnitureWiredVariableLogic? GetSharedVariable() =>
        GetVariableBoxOnTile() is { IsShared: true } variable ? variable : null;
}
