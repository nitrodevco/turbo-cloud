using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredVariableBinding
{
    public WiredVariableTargetType Target { get; }
    public int? TargetId { get; }
}
