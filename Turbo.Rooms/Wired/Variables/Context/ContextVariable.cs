using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// A variable about the stack running right now rather than about anything standing in the
/// room: it is read from that execution (what its selectors picked, its signal, the event that
/// started it), and is the default outside
/// one. The client lists them all the time, because they declare
/// <see cref="WiredVariableFlags.AlwaysAvailable"/>.
/// </summary>
public abstract class ContextVariable(RoomGrain roomGrain) : WiredInternalVariable(roomGrain)
{
    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.Context;

    public override bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        if (!CanBind(key))
            return false;

        if (_roomGrain.WiredSystem.CurrentExecution is { } execution)
            value = GetValueForExecution(execution);

        return true;
    }

    /// <summary>The value for the wired execution running now.</summary>
    protected abstract WiredVariableValue GetValueForExecution(WiredRunningExecution execution);
}
