using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariable
{
    public bool TryGet(in WiredVariableBinding binding, out int value);
    public Task<bool> GiveValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value,
        bool replace = false
    );
    public Task<bool> SetValueAsync(
        WiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    );
    public bool RemoveValue(WiredVariableBinding binding);

    public WiredVariableKey GetVariableKey();
    public WiredVariableSnapshot GetVarSnapshot();
}
