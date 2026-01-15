using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables;

public abstract class WiredVariable(RoomGrain roomGrain) : IWiredVariable
{
    protected readonly RoomGrain _roomGrain = roomGrain;

    public abstract IWiredVariableDefinition VarDefinition { get; }
    public required IStorageData StorageData { get; init; }

    public virtual bool CanBind(in IWiredVariableBinding binding) => false;

    public virtual bool TryGet(
        in IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        out int value
    )
    {
        value = 0;

        return false;
    }

    public virtual bool SetValue(
        in IWiredVariableBinding binding,
        IWiredExecutionContext ctx,
        int value
    ) => false;

    public virtual bool RemoveValue(string key) => false;

    public override int GetHashCode() => VarDefinition.GetHashCode();

    public WiredVariableSnapshot GetVarSnapshot() => VarDefinition.GetSnapshot();
}
