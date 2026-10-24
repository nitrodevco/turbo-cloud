using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

/// <summary>
/// The daily task variable (<c>wf_var_dailytask</c>, Flash <c>VariableCodes</c> 8, editor
/// <c>wired_setup.variables.DailyTaskVariable</c>): a user variable named in the editor's
/// variable name section, with a "Daily task code:" text input (up to 100 characters), saved as
/// "name\tcode".
/// </summary>
[RoomObjectLogic("wf_var_dailytask")]
public class WiredVariableDailyTask(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredCodedVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredVariableBoxType.DailyTask;

    // TextInputParam("", 100, "1234..").
    protected override int CodeMaxLength => 100;

    /// <summary>The daily task code, the string param's second part.</summary>
    public string TaskCode => Code;
}
