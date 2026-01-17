using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired.Variables;

public readonly record struct WiredVariableKey(WiredVariableTargetType TargetType, string Key);
