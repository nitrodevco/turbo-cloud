using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableBinding
{
    public required WiredVariableTargetType Target { get; init; }
    public int? TargetId { get; init; }
}
