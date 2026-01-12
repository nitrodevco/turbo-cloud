using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.IntParams;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_room")]
public class WiredVariableRoom(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableType.GLOBAL_VARIABLE;

    public override List<IWiredIntParamRule> GetIntParamRules() =>
        [
            new WiredIntRangeRule(0, 7, 0), // Movement Type
            new WiredIntRangeRule(0, 3, 0), // Rotation Type
        ];
}
