using System.Threading.Tasks;
using Turbo.Primitives.Furniture;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariable
{
    public IWiredVariableDefinition VarDefinition { get; }
    public IStorageData StorageData { get; }

    public bool CanBind(in IWiredVariableBinding binding);
    public bool TryGet(in IWiredVariableBinding binding, IWiredExecutionContext ctx, out int value);
    public Task<bool> SetValueAsync(
        IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    );
    public bool RemoveValue(string key);
}
