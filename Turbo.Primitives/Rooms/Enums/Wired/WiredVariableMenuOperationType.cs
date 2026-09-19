namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// What the wired menu's inspection tab asks for on one furni or user, as the client numbers it
/// in the last field of <c>WiredSetObjectVariableValue</c>: edit a value it already holds, give
/// it the variable, or take the variable away.
/// </summary>
public enum WiredVariableMenuOperationType
{
    SetValue = 0,
    Create = 1,
    Delete = 2,
}
