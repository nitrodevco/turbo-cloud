using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariable : IWiredVariableStore
{
    public bool CanBind(in WiredVariableKey key);
    public WiredVariableSnapshot GetVarSnapshot();
}
