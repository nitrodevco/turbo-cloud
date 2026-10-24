using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

/// <summary>
/// The quest variable (<c>wf_var_quest</c>, editor <c>wired_setup.variables.QuestVariable</c>):
/// a user variable named in the variable name section, with a "Quest name" text input (up to 500
/// characters, <c>TextInputParam("", 500)</c>), saved as "name\tquest".
/// </summary>
[RoomObjectLogic("wf_var_quest")]
public class WiredVariableQuest(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredCodedVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.Quest;

    protected override int CodeMaxLength => 500;
}
