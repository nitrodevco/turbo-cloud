using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariable
{
    public IStorageData StorageData { get; }

    public bool CanBind(in IWiredVariableBinding binding);
    public bool TryGet(in IWiredVariableBinding binding, out int value);
    public Task<bool> SetValueAsync(
        IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    );
    public bool RemoveValue(string key);

    public WiredVariableKey GetVariableKey();
    public WiredVariableSnapshot GetVarSnapshot();
}
