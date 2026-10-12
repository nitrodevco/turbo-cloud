using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired.Variables;

/// <summary>
/// Which variables only have a value while a stack runs. A context variable is read from the
/// execution running it (its selectors, its signal, the event that started it), so outside one
/// it has nothing to show, and the client keeps it out of every picker that reads a variable
/// at another time (AS3 <c>WiredVariable.requiresExecutionContext</c>, read by
/// <c>EchoVariable</c>, <c>VariableUpdate</c> and the fx audience). Which variables the hotel
/// marks is not in the client; the context ones are this server's reading of the name.
/// </summary>
public static class WiredVariableExecutionContext
{
    public static WiredVariableFlags WithRequirement(
        WiredVariableFlags flags,
        WiredVariableTargetType targetType
    ) =>
        targetType == WiredVariableTargetType.Context
            ? flags | WiredVariableFlags.RequiresExecutionContext
            : flags;
}
