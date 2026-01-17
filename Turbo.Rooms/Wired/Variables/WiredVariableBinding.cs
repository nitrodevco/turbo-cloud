using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

public readonly record struct WiredVariableBinding(WiredVariableTargetType Target, int TargetId)
    : IWiredVariableBinding;
