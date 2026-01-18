using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariableBinding
{
    public WiredVariableTargetType TargetType { get; }
    public int TargetId { get; }
}
