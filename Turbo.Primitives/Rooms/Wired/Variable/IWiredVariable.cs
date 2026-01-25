using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariable
{
    public bool CanBind(in WiredVariableBinding binding);
    public bool TryGet(in WiredVariableBinding binding, out int value);
    public Task<bool> SetValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    );
    public bool RemoveValue(WiredVariableBinding binding);

    public WiredVariableKey GetVariableKey();
    public WiredVariableSnapshot GetVarSnapshot();
}
