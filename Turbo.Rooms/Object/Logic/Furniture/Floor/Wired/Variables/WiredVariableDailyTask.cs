using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

/// <summary>
/// The daily task variable (<c>wf_var_dailytask</c>, Flash <c>VariableCodes</c> 8, editor
/// <c>wired_setup.variables.DailyTaskVariable</c>): a user variable named in the editor's
/// variable name section, with a "Daily task code:" text input (up to 100 characters), saved as
/// "name\tcode". The name is the variable's: the code after the tab is not part of it.
/// <para>
/// Habbo lists the furni without a name or an offer, so what it holds at runtime is not known;
/// like the quest variables it is declared with its name and holds nothing until it is known.
/// </para>
/// </summary>
[RoomObjectLogic("wf_var_dailytask")]
public class WiredVariableDailyTask(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    private const char SEPARATOR = '\t';

    // The editor's limits: VariableNameSection (40) and TextInputParam("", 100, "1234..").
    private const int NAME_MAX_LENGTH = 40;
    private const int CODE_MAX_LENGTH = 100;

    public override int WiredCode => (int)WiredVariableBoxType.DailyTask;

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.User;
    protected override WiredAvailabilityType AvailabilityType => WiredAvailabilityType.Unknown;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.CanInterceptChanges
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.CanReadLastUpdateTime;

    // The name section and the code input, each within its input's length.
    protected override int GetStringParamMaxLength() => NAME_MAX_LENGTH + 1 + CODE_MAX_LENGTH;

    /// <summary>The daily task code, the string param's second part.</summary>
    public string TaskCode
    {
        get
        {
            var stringParam = _wiredData.StringParam ?? string.Empty;
            var index = stringParam.IndexOf(SEPARATOR);
            var code = index < 0 ? string.Empty : stringParam[(index + 1)..];

            return code.Length > CODE_MAX_LENGTH ? code[..CODE_MAX_LENGTH] : code;
        }
    }

    protected override WiredVariableSnapshot BuildVarSnapshot() =>
        base.BuildVarSnapshot() with
        {
            VariableName = NameOf(_wiredData.StringParam),
        };

    private static string NameOf(string? stringParam)
    {
        var value = stringParam ?? string.Empty;
        var index = value.IndexOf(SEPARATOR);
        var name = index < 0 ? value : value[..index];

        return name.Length > NAME_MAX_LENGTH ? name[..NAME_MAX_LENGTH] : name;
    }
}
