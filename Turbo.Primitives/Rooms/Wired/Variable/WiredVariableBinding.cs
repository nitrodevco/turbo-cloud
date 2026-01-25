using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired.Variable;

[GenerateSerializer, Immutable]
public readonly record struct WiredVariableBinding(WiredVariableTargetType TargetType, int TargetId)
{
    public override string ToString() => $"{TargetType}:{TargetId}";
}
