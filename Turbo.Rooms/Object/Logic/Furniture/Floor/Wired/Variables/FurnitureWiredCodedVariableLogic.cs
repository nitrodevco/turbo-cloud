using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

/// <summary>
/// A user variable box whose editor has the variable name section and one text input after it,
/// saved as "name\tcode" (AS3 WIN63-202610201032 <c>wired_setup/variables</c>: the quest, quest
/// chain and daily task variables, <c>STRING_PARAM_SPLITTER</c> "\t", each <c>variableType()</c>
/// <c>USER</c>). The name is the variable's: the code after the tab is not part of it.
/// <para>
/// Habbo lists these furni without a name or an offer, so what they hold at runtime is not known;
/// each is declared with its name and holds nothing until it is known.
/// </para>
/// </summary>
public abstract class FurnitureWiredCodedVariableLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableLogic(grainFactory, stuffDataFactory, ctx)
{
    private const char SEPARATOR = '\t';

    // The editor's VariableNameSection.
    private const int NAME_MAX_LENGTH = 40;

    /// <summary>The length of the editor's code input (<c>TextInputParam("", n)</c>).</summary>
    protected abstract int CodeMaxLength { get; }

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.User;
    protected override WiredAvailabilityType AvailabilityType => WiredAvailabilityType.Unknown;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.CanInterceptChanges
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.CanReadLastUpdateTime;

    // The name section and the code input, each within its input's length.
    protected override int GetStringParamMaxLength() => NAME_MAX_LENGTH + 1 + CodeMaxLength;

    /// <summary>The code, the string param's second part.</summary>
    public string Code
    {
        get
        {
            var stringParam = _wiredData.StringParam ?? string.Empty;
            var index = stringParam.IndexOf(SEPARATOR);
            var code = index < 0 ? string.Empty : stringParam[(index + 1)..];

            return code.Length > CodeMaxLength ? code[..CodeMaxLength] : code;
        }
    }

    protected override WiredVariableSnapshot BuildVarSnapshot() =>
        base.BuildVarSnapshot() with
        {
            VariableName = NameOf(_wiredData.StringParam),
        };

    /// <summary>The name, the part before the tab: the code is not part of it.</summary>
    protected override string VariableNameOf(string? stringParam)
    {
        var value = stringParam ?? string.Empty;
        var index = value.IndexOf(SEPARATOR);

        return index < 0 ? value : value[..index];
    }

    protected override string WithVariableName(string? stringParam, string name)
    {
        var value = stringParam ?? string.Empty;
        var index = value.IndexOf(SEPARATOR);

        return index < 0 ? name : name + value[index..];
    }

    private string NameOf(string? stringParam)
    {
        var name = VariableNameOf(stringParam);

        return name.Length > NAME_MAX_LENGTH ? name[..NAME_MAX_LENGTH] : name;
    }
}
