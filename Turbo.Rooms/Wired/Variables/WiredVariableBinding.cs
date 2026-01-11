using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableBinding : IWiredVariableBinding
{
    public required WiredVariableTargetType Target { get; init; }
    public int? TargetId { get; init; }
}
