using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// A variable about the stack running right now rather than about anything standing in the
/// room. What it is worth comes from the execution context, which this lookup does not carry,
/// so every context variable reports the default until one does; the client still lists them,
/// because they declare <see cref="WiredVariableFlags.AlwaysAvailable"/>.
/// </summary>
public abstract class ContextVariable(RoomGrain roomGrain) : WiredInternalVariable(roomGrain)
{
    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.Context;

    public override bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        return CanBind(key);
    }
}
