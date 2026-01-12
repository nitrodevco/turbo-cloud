using Turbo.Primitives.Furniture;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredVariable
{
    public IWiredVariableDefinition VarDefinition { get; }
    public IStorageData StorageData { get; }

    public bool CanBind(in IWiredVariableBinding binding);
    public bool TryGet(in IWiredVariableBinding binding, IWiredExecutionContext ctx, out int value);
    public bool SetValue(in IWiredVariableBinding binding, IWiredExecutionContext ctx, int value);
    public bool RemoveValue(string key);
    public int GetHashCode();
}
