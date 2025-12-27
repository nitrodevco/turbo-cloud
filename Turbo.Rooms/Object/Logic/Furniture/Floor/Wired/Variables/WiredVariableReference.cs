using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

[RoomObjectLogic("wf_var_reference")]
public class WiredVariableReference(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
)
    : FurnitureWiredVariableLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx),
        IWiredVariable
{
    public override int WiredCode => (int)WiredVariableType.REFERENCE_VARIABLE;

    public override void Apply(IWiredContext ctx) { }
}
