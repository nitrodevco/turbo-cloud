using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariableStore
{
    public bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value);
    public Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    );
    public Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    );
    public bool RemoveValue(WiredVariableKey key);
}
