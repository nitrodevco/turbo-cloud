using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

/// <summary>
/// The quest chain variable (<c>wf_var_quest_chain</c>, editor
/// <c>wired_setup.variables.QuestChainVariable</c>): a user variable named in the variable name
/// section, with a "Quest chain name" text input (up to 500 characters,
/// <c>TextInputParam("", 500)</c>), saved as "name\tchain".
/// </summary>
[RoomObjectLogic("wf_var_quest_chain")]
public class WiredVariableQuestChain(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredCodedVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.QuestChain;

    protected override int CodeMaxLength => 500;
}
