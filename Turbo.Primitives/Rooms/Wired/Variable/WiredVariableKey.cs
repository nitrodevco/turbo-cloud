using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public readonly record struct WiredVariableKey(
    WiredVariableTargetType TargetType,
    string VariableName
);
